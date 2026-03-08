#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SolarTerminal.Data;

namespace SolarTerminal.Editor
{
    /// <summary>
    /// Bulk importer for celestial body definitions.
    ///
    /// Reads a JSON file (OrbitalSystemImportData format) and:
    ///   - Creates new CelestialBodyDefinition assets for unknown ids
    ///   - Updates existing assets by id (preserves manual prefab assignments)
    ///   - Optionally applies BodyTypePreset defaults
    ///   - Optionally updates OrbitalSystemDefinition body list
    ///
    /// Access via:  Tools > SolarTerminal > Import Orbital Bodies
    ///
    /// Source-of-truth workflow:
    ///   1. Edit bodies.json (or export from spreadsheet)
    ///   2. Run importer
    ///   3. Assign prefabs manually for new bodies (or add presets with default prefabs)
    ///   4. Validate with OrbitalSystemValidator
    ///   5. Press Play
    /// </summary>
    public static class OrbitalBodyImporter
    {
        private const string MENU_ITEM          = "Tools/SolarTerminal/Import Orbital Bodies";
        private const string MENU_APPLY_PRESETS = "Tools/SolarTerminal/Apply Presets to All Bodies";
        private const string OUTPUT_ROOT        = "Assets/Data/Bodies";

        // ------------------------------------------------------------------
        // Menu entry point
        // ------------------------------------------------------------------

        [MenuItem(MENU_ITEM)]
        public static void RunImport()
        {
            string jsonPath = EditorUtility.OpenFilePanel(
                "Select bodies JSON file", Application.dataPath, "json");

            if (string.IsNullOrEmpty(jsonPath)) return;

            string json;
            try { json = File.ReadAllText(jsonPath); }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Error",
                    $"Could not read file:\n{ex.Message}", "OK");
                return;
            }

            OrbitalSystemImportData data;
            try { data = JsonUtility.FromJson<OrbitalSystemImportData>(json); }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Error",
                    $"JSON parse failed:\n{ex.Message}", "OK");
                return;
            }

            if (data.bodies == null || data.bodies.Length == 0)
            {
                EditorUtility.DisplayDialog("Import", "No bodies found in JSON.", "OK");
                return;
            }

            // Ask user where to save assets
            string outputFolder = EditorUtility.OpenFolderPanel(
                "Select output folder for definition assets",
                "Assets/Data/Bodies", "");

            if (string.IsNullOrEmpty(outputFolder))
            {
                outputFolder = OUTPUT_ROOT;
                EnsureFolder(outputFolder);
            }

            // Convert absolute path to project-relative
            if (outputFolder.StartsWith(Application.dataPath))
                outputFolder = "Assets" + outputFolder.Substring(Application.dataPath.Length);

            var result = ImportBodies(data, outputFolder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Offer to update / create OrbitalSystemDefinition
            if (!string.IsNullOrEmpty(data.systemId))
                OfferSystemDefinitionUpdate(data, outputFolder, result.importedDefs);

            string summary =
                $"Import complete.\n\n" +
                $"Created:  {result.created}\n" +
                $"Updated:  {result.updated}\n" +
                $"Skipped:  {result.skipped}\n\n" +
                (result.warnings.Count > 0
                    ? $"Warnings ({result.warnings.Count}):\n" + string.Join("\n", result.warnings)
                    : "No warnings.");

            Debug.Log("[OrbitalBodyImporter] " + summary);
            EditorUtility.DisplayDialog("Import Complete", summary, "OK");
        }

        // ------------------------------------------------------------------
        // Core import logic — also callable from other editor tools
        // ------------------------------------------------------------------

        public struct ImportResult
        {
            public int                        created;
            public int                        updated;
            public int                        skipped;
            public List<string>               warnings;
            public List<CelestialBodyDefinition> importedDefs;
        }

        public static ImportResult ImportBodies(
            OrbitalSystemImportData data,
            string                  outputFolder)
        {
            var result = new ImportResult
            {
                warnings     = new List<string>(),
                importedDefs = new List<CelestialBodyDefinition>(),
            };

            // Cache existing definition assets by id for fast lookup
            var existingById = BuildExistingAssetIndex();

            // Cache presets by asset name for optional preset application
            var presetsByKey = BuildPresetIndex();

            EnsureFolder(outputFolder);

            foreach (var record in data.bodies)
            {
                if (string.IsNullOrWhiteSpace(record.id))
                {
                    result.warnings.Add("Skipped record with empty id.");
                    result.skipped++;
                    continue;
                }

                bool isNew = !existingById.TryGetValue(record.id, out var def);

                if (isNew)
                {
                    def      = ScriptableObject.CreateInstance<CelestialBodyDefinition>();
                    def.name = $"Body_{SanitizeFileName(record.id)}";
                    string assetPath = $"{outputFolder}/{def.name}.asset";
                    AssetDatabase.CreateAsset(def, assetPath);
                    result.created++;
                }
                else
                {
                    result.updated++;
                }

                // Apply preset defaults BEFORE record values so record can override.
                // For existing assets: only fills empty prefab slots (preserves manual assignments).
                // For new assets: fills everything from preset.
                if (!string.IsNullOrEmpty(record.presetKey) &&
                    presetsByKey.TryGetValue(record.presetKey, out var preset))
                {
                    preset.ApplyDefaults(def);
                }

                // Write all non-visual fields unconditionally
                ApplyRecordToDefinition(record, def);

                // Resolve prefab paths only for new assets
                // (existing assets keep their manually assigned prefabs)
                if (isNew)
                {
                    TryResolvePrefab(record.nearPrefabPath,   ref def.nearPrefab,   record.id, "near",   result.warnings);
                    TryResolvePrefab(record.mediumPrefabPath, ref def.mediumPrefab, record.id, "medium", result.warnings);
                    TryResolvePrefab(record.farPrefabPath,    ref def.farPrefab,    record.id, "far",    result.warnings);
                }

                EditorUtility.SetDirty(def);
                result.importedDefs.Add(def);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Field mapping: record → definition
        // ------------------------------------------------------------------

        private static void ApplyRecordToDefinition(
            BodyImportRecord        record,
            CelestialBodyDefinition def)
        {
            def.id             = record.id;
            def.parentId       = record.parentId;
            def.displayNameKey = record.displayNameKey;

            if (TryParseBodyType(record.bodyType, out var bt))
                def.bodyType = bt;

            def.showInHierarchy = record.showInHierarchy;
            def.sortOrder       = record.sortOrder;
            def.isSelectable    = record.isSelectable;
            def.visualRadius    = Mathf.Max(0.01f, record.visualRadius);

            // Orbital elements
            def.semiMajorAxis            = record.semiMajorAxis;
            def.eccentricity             = Mathf.Clamp(record.eccentricity, 0f, 0.99f);
            def.inclination              = record.inclination;
            def.longitudeOfAscendingNode = record.longitudeOfAscendingNode;
            def.argumentOfPeriapsis      = record.argumentOfPeriapsis;
            def.meanAnomalyAtEpoch       = record.meanAnomalyAtEpoch;
            def.orbitalPeriod            = record.orbitalPeriod;
        }

        // ------------------------------------------------------------------
        // Prefab resolution via AssetDatabase (project-relative or Resources path)
        // ------------------------------------------------------------------

        private static void TryResolvePrefab(
            string         path,
            ref GameObject field,
            string         bodyId,
            string         slot,
            List<string>   warnings)
        {
            if (string.IsNullOrEmpty(path)) return;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                go = Resources.Load<GameObject>(path);

            if (go != null)
                field = go;
            else
                warnings.Add($"[{bodyId}] Could not resolve {slot} prefab: '{path}'");
        }

        // ------------------------------------------------------------------
        // Asset indexing helpers
        // ------------------------------------------------------------------

        private static Dictionary<string, CelestialBodyDefinition> BuildExistingAssetIndex()
        {
            var map  = new Dictionary<string, CelestialBodyDefinition>(StringComparer.Ordinal);
            var guids = AssetDatabase.FindAssets("t:CelestialBodyDefinition");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def  = AssetDatabase.LoadAssetAtPath<CelestialBodyDefinition>(path);
                if (def != null && !string.IsNullOrEmpty(def.id))
                    map.TryAdd(def.id, def);
            }
            return map;
        }

        private static Dictionary<string, BodyTypePreset> BuildPresetIndex()
        {
            var map   = new Dictionary<string, BodyTypePreset>(StringComparer.OrdinalIgnoreCase);
            var guids = AssetDatabase.FindAssets("t:BodyTypePreset");
            foreach (var guid in guids)
            {
                var path   = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<BodyTypePreset>(path);
                if (preset != null)
                    map.TryAdd(preset.name, preset);
            }
            return map;
        }

        // ------------------------------------------------------------------
        // OrbitalSystemDefinition update
        // ------------------------------------------------------------------

        private static void OfferSystemDefinitionUpdate(
            OrbitalSystemImportData      data,
            string                       folder,
            List<CelestialBodyDefinition> defs)
        {
            bool update = EditorUtility.DisplayDialog(
                "Update System Definition?",
                $"Create or update OrbitalSystemDefinition for system '{data.systemId}'?",
                "Yes", "No");

            if (!update) return;

            string sysPath = $"{folder}/System_{SanitizeFileName(data.systemId)}.asset";
            var    sysDef  = AssetDatabase.LoadAssetAtPath<OrbitalSystemDefinition>(sysPath);

            if (sysDef == null)
            {
                sysDef = ScriptableObject.CreateInstance<OrbitalSystemDefinition>();
                AssetDatabase.CreateAsset(sysDef, sysPath);
            }

            sysDef.systemId       = data.systemId;
            sysDef.displayNameKey = data.displayNameKey;
            sysDef.centralBodyId  = data.centralBodyId;
            sysDef.bodies         = new List<CelestialBodyDefinition>(defs);

            EditorUtility.SetDirty(sysDef);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OrbitalBodyImporter] System definition saved: {sysPath}");
        }

        // ------------------------------------------------------------------
        // Apply presets to existing assets
        // ------------------------------------------------------------------

        /// <summary>
        /// Iterates every CelestialBodyDefinition in the project.
        /// For each that has a sourcePreset assigned AND a missing nearPrefab,
        /// copies the preset's defaultNearPrefab / defaultFarPrefab into the asset.
        ///
        /// Safe to run multiple times — only fills empty slots, never overwrites.
        ///
        /// Access via: Tools > SolarTerminal > Apply Presets to All Bodies
        /// </summary>
        [MenuItem(MENU_APPLY_PRESETS)]
        public static void ApplyPresetsToAll()
        {
            var guids = AssetDatabase.FindAssets("t:CelestialBodyDefinition");
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Apply Presets", "No CelestialBodyDefinition assets found.", "OK");
                return;
            }

            int filled = 0, skipped = 0;
            var log = new System.Text.StringBuilder();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def  = AssetDatabase.LoadAssetAtPath<CelestialBodyDefinition>(path);
                if (def == null) continue;

                var preset = def.sourcePreset;
                if (preset == null) { skipped++; continue; }

                bool changed = false;

                if (def.nearPrefab == null && preset.defaultNearPrefab != null)
                {
                    def.nearPrefab = preset.defaultNearPrefab;
                    log.AppendLine($"  [{def.id}] nearPrefab ← {preset.defaultNearPrefab.name}");
                    changed = true;
                }

                if (def.farPrefab == null && preset.defaultFarPrefab != null)
                {
                    def.farPrefab = preset.defaultFarPrefab;
                    log.AppendLine($"  [{def.id}] farPrefab ← {preset.defaultFarPrefab.name}");
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(def);
                    filled++;
                }
                else
                {
                    skipped++;
                }
            }

            AssetDatabase.SaveAssets();

            string summary = $"Apply Presets done.\n\nFilled: {filled}\nSkipped (already set or no preset): {skipped}";
            if (log.Length > 0) summary += "\n\nDetails:\n" + log;

            Debug.Log("[OrbitalBodyImporter] " + summary);
            EditorUtility.DisplayDialog("Apply Presets", summary, "OK");
        }

        // ------------------------------------------------------------------
        // Utility
        // ------------------------------------------------------------------

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            var parts  = folderPath.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static string SanitizeFileName(string id)
            => id.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');

        private static bool TryParseBodyType(string s, out BodyType result)
            => Enum.TryParse(s, ignoreCase: true, out result);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// CUSTOM INSPECTOR — CelestialBodyDefinition
// Adds "Apply Preset" button and prefab status directly in the asset editor.
// ══════════════════════════════════════════════════════════════════════════

namespace SolarTerminal.Editor
{
    using UnityEditor;
    using UnityEngine;
    using SolarTerminal.Data;

    [CustomEditor(typeof(CelestialBodyDefinition))]
    public class CelestialBodyDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var def = (CelestialBodyDefinition)target;

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Prefab Status", EditorStyles.boldLabel);

            DrawPrefabStatus("Near prefab",   def.ResolvedNearPrefab);
            DrawPrefabStatus("Medium prefab", def.mediumPrefab, optional: true);
            DrawPrefabStatus("Far prefab",    def.farPrefab,    optional: true,
                             fallbackNote: "(fallback marker will be generated)");

            GUILayout.Space(6);

            // Apply preset button — only if sourcePreset is assigned
            if (def.sourcePreset != null)
            {
                if (GUILayout.Button($"↓  Apply preset defaults: {def.sourcePreset.presetLabel}",
                    GUILayout.Height(26)))
                {
                    def.sourcePreset.ApplyDefaults(def);
                    EditorUtility.SetDirty(def);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[Inspector] Preset '{def.sourcePreset.presetLabel}' applied to '{def.id}'.");
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No sourcePreset assigned. Assign a preset above, then click Apply.",
                    MessageType.None);
            }
        }

        private static void DrawPrefabStatus(
            string label, GameObject prefab,
            bool optional = false, string fallbackNote = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string status;
                Color  col;

                if (prefab != null)
                {
                    status = $"✓  {prefab.name}";
                    col    = new Color(0.3f, 0.8f, 0.3f);
                }
                else if (optional)
                {
                    status = fallbackNote ?? "— (optional, will reuse near)";
                    col    = new Color(0.7f, 0.7f, 0.7f);
                }
                else
                {
                    status = "✗  MISSING";
                    col    = new Color(1f, 0.4f, 0.3f);
                }

                var prev = GUI.contentColor;
                GUI.contentColor = col;
                EditorGUILayout.LabelField(label, status);
                GUI.contentColor = prev;
            }
        }
    }
}
#endif
