#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SolarTerminal.Data;

namespace SolarTerminal.Editor
{
    /// <summary>
    /// Editor-side validator for orbital map data.
    /// Detects common content errors before runtime.
    ///
    /// Access via:
    ///   Tools > SolarTerminal > Validate Orbital System
    ///
    /// Can also be called programmatically from other editor tools.
    /// </summary>
    public static class OrbitalSystemValidator
    {
        private const string MENU_ITEM = "Tools/SolarTerminal/Validate Orbital System";

        // ══════════════════════════════════════════════════════════════════
        // MENU ENTRY POINT
        // ══════════════════════════════════════════════════════════════════

        [MenuItem(MENU_ITEM)]
        public static void RunFromMenu()
        {
            // Find the OrbitalSystemDefinition in the scene or let user pick
            var systemDef = FindSystemDefinitionInScene()
                         ?? PickSystemDefinition();

            if (systemDef == null)
            {
                EditorUtility.DisplayDialog("Validate", "No OrbitalSystemDefinition found.", "OK");
                return;
            }

            var report = Validate(systemDef.bodies);
            ShowReport(report, systemDef.name);
        }

        // ══════════════════════════════════════════════════════════════════
        // PUBLIC API — callable from other tools / custom inspectors
        // ══════════════════════════════════════════════════════════════════

        public struct ValidationReport
        {
            public List<string> errors;
            public List<string> warnings;
            public bool         IsValid => errors.Count == 0;
        }

        /// <summary>
        /// Validate a list of body definitions.
        /// Returns a report with all errors and warnings found.
        /// </summary>
        public static ValidationReport Validate(
            IReadOnlyList<CelestialBodyDefinition> bodies)
        {
            var report = new ValidationReport
            {
                errors   = new List<string>(),
                warnings = new List<string>(),
            };

            if (bodies == null || bodies.Count == 0)
            {
                report.errors.Add("Body list is null or empty.");
                return report;
            }

            var ids    = new HashSet<string>();
            var idSet  = new HashSet<string>(); // all non-null ids for parent lookup

            // ── Pass 1: collect all ids ────────────────────────────────
            foreach (var def in bodies)
            {
                if (def == null) { report.errors.Add("Null entry in body list."); continue; }
                if (!string.IsNullOrWhiteSpace(def.id))
                    idSet.Add(def.id);
            }

            // ── Pass 2: per-body checks ────────────────────────────────
            foreach (var def in bodies)
            {
                if (def == null) continue;
                string label = $"[{def.id ?? def.name ?? "?"}]";

                CheckIdentity(def, label, ids, report);
                CheckHierarchy(def, label, idSet, report);
                CheckOrbitalValues(def, label, report);
                CheckRepresentations(def, label, report);
                CheckUIMetadata(def, label, report);
            }

            // ── Pass 3: cycle detection ────────────────────────────────
            CheckForCycles(bodies, report);

            return report;
        }

        // ══════════════════════════════════════════════════════════════════
        // INDIVIDUAL CHECK GROUPS
        // ══════════════════════════════════════════════════════════════════

        private static void CheckIdentity(
            CelestialBodyDefinition def,
            string                  label,
            HashSet<string>         seenIds,
            ValidationReport        report)
        {
            // Empty id
            if (string.IsNullOrWhiteSpace(def.id))
            {
                report.errors.Add($"{label} id is empty.");
                return;
            }

            // Duplicate id
            if (!seenIds.Add(def.id))
                report.errors.Add($"{label} DUPLICATE id '{def.id}'.");

            // Missing displayNameKey
            if (string.IsNullOrWhiteSpace(def.displayNameKey))
                report.warnings.Add($"{label} displayNameKey is empty — body will show raw id in UI.");
        }

        private static void CheckHierarchy(
            CelestialBodyDefinition def,
            string                  label,
            HashSet<string>         allIds,
            ValidationReport        report)
        {
            bool isRoot = string.IsNullOrWhiteSpace(def.parentId);

            if (!isRoot && !allIds.Contains(def.parentId))
                report.errors.Add(
                    $"{label} parentId '{def.parentId}' does not match any known body id.");

            // Root body should be a star
            if (isRoot && def.bodyType != BodyType.Star)
                report.warnings.Add(
                    $"{label} is a root body (no parentId) but bodyType is {def.bodyType}, not Star.");
        }

        private static void CheckOrbitalValues(
            CelestialBodyDefinition def,
            string                  label,
            ValidationReport        report)
        {
            bool isRoot = string.IsNullOrWhiteSpace(def.parentId);

            if (isRoot)
            {
                // Root: orbital params should be zero
                if (def.semiMajorAxis > 0f || def.orbitalPeriod > 0f)
                    report.warnings.Add(
                        $"{label} is a root body but has non-zero orbital parameters.");
                return;
            }

            // Non-root: must have valid orbit
            if (def.semiMajorAxis <= 0f)
                report.errors.Add($"{label} semiMajorAxis is 0 or negative.");

            if (def.orbitalPeriod <= 0f)
                report.errors.Add($"{label} orbitalPeriod is 0 or negative.");

            if (def.eccentricity < 0f || def.eccentricity >= 1f)
                report.errors.Add(
                    $"{label} eccentricity {def.eccentricity} is out of range [0, 1).");

            // Sanity: very small or very large values
            if (def.semiMajorAxis > 0f && def.semiMajorAxis < 0.01f)
                report.warnings.Add(
                    $"{label} semiMajorAxis {def.semiMajorAxis} is suspiciously small.");
        }

        private static void CheckRepresentations(
            CelestialBodyDefinition def,
            string                  label,
            ValidationReport        report)
        {
            // ResolvedNearPrefab covers both nearPrefab and legacy prefab field
            if (def.ResolvedNearPrefab == null)
                report.warnings.Add(
                    $"{label} has no near prefab assigned — body will be invisible up close.");
        }

        private static void CheckUIMetadata(
            CelestialBodyDefinition def,
            string                  label,
            ValidationReport        report)
        {
            if (def.sortOrder < 0)
                report.warnings.Add($"{label} sortOrder {def.sortOrder} is negative.");
        }

        // ══════════════════════════════════════════════════════════════════
        // CYCLE DETECTION
        // Uses DFS with three-colour marking (white / grey / black).
        // ══════════════════════════════════════════════════════════════════

        private static void CheckForCycles(
            IReadOnlyList<CelestialBodyDefinition> bodies,
            ValidationReport                       report)
        {
            var byId = new Dictionary<string, CelestialBodyDefinition>();
            foreach (var def in bodies)
                if (def != null && !string.IsNullOrEmpty(def.id))
                    byId.TryAdd(def.id, def);

            var white = new HashSet<string>(byId.Keys);
            var grey  = new HashSet<string>();
            var black = new HashSet<string>();

            foreach (var id in new List<string>(byId.Keys))
            {
                if (white.Contains(id))
                    DfsCheckCycle(id, byId, white, grey, black, report);
            }
        }

        private static void DfsCheckCycle(
            string                                       startId,
            Dictionary<string, CelestialBodyDefinition> byId,
            HashSet<string>                              white,
            HashSet<string>                              grey,
            HashSet<string>                              black,
            ValidationReport                             report)
        {
            if (!byId.TryGetValue(startId, out var def)) return;

            white.Remove(startId);
            grey.Add(startId);

            var parentId = def.parentId;
            if (!string.IsNullOrEmpty(parentId) && byId.ContainsKey(parentId))
            {
                if (grey.Contains(parentId))
                {
                    report.errors.Add(
                        $"CYCLIC HIERARCHY detected: '{startId}' → '{parentId}' creates a loop.");
                }
                else if (white.Contains(parentId))
                {
                    DfsCheckCycle(parentId, byId, white, grey, black, report);
                }
            }

            grey.Remove(startId);
            black.Add(startId);
        }

        // ══════════════════════════════════════════════════════════════════
        // REPORT DISPLAY
        // ══════════════════════════════════════════════════════════════════

        private static void ShowReport(ValidationReport report, string sourceName)
        {
            string header = $"Validation: {sourceName}\n" +
                            $"Bodies checked.  " +
                            $"Errors: {report.errors.Count}  " +
                            $"Warnings: {report.warnings.Count}\n";

            if (report.errors.Count > 0)
            {
                string errorBlock = string.Join("\n", report.errors);
                Debug.LogError("[OrbitalSystemValidator] ERRORS:\n" + errorBlock);
            }

            if (report.warnings.Count > 0)
            {
                string warnBlock = string.Join("\n", report.warnings);
                Debug.LogWarning("[OrbitalSystemValidator] WARNINGS:\n" + warnBlock);
            }

            if (report.IsValid)
                Debug.Log("[OrbitalSystemValidator] All checks passed. ✓");

            string dialogBody = report.IsValid
                ? header + "✓ All checks passed."
                : header +
                  (report.errors.Count   > 0 ? $"\nERRORS:\n{string.Join("\n", report.errors)}"    : "") +
                  (report.warnings.Count > 0 ? $"\nWARNINGS:\n{string.Join("\n", report.warnings)}" : "");

            // Truncate very long dialogs
            if (dialogBody.Length > 2000)
                dialogBody = dialogBody.Substring(0, 2000) + "\n...(see Console for full report)";

            EditorUtility.DisplayDialog(
                report.IsValid ? "Validation Passed ✓" : "Validation Failed ✗",
                dialogBody,
                "OK");
        }

        // ══════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════

        private static OrbitalSystemDefinition FindSystemDefinitionInScene()
        {
            // Look in assets loaded in current scene context via Bootstrap
            var bootstrap = Object.FindFirstObjectByType<SolarTerminal.Bootstrap.OrbitalMapBootstrap>();
            return bootstrap != null ? bootstrap.SystemDefinition : null;
        }

        private static OrbitalSystemDefinition PickSystemDefinition()
        {
            var guids = AssetDatabase.FindAssets("t:OrbitalSystemDefinition");
            if (guids.Length == 0) return null;
            if (guids.Length == 1)
                return AssetDatabase.LoadAssetAtPath<OrbitalSystemDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            // Multiple systems — let user pick
            var options = new string[guids.Length];
            var paths   = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                paths[i]   = AssetDatabase.GUIDToAssetPath(guids[i]);
                options[i] = System.IO.Path.GetFileNameWithoutExtension(paths[i]);
            }
            int choice = EditorUtility.DisplayDialogComplex(
                "Select System", "Multiple systems found. Pick one to validate.",
                options.Length > 0 ? options[0] : "First",
                options.Length > 1 ? options[1] : "Cancel",
                "Cancel");

            if (choice == 2) return null;
            return AssetDatabase.LoadAssetAtPath<OrbitalSystemDefinition>(paths[choice]);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // CUSTOM INSPECTOR BUTTON for OrbitalSystemDefinition
    // ══════════════════════════════════════════════════════════════════════

    [CustomEditor(typeof(OrbitalSystemDefinition))]
    public class OrbitalSystemDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8);

            if (GUILayout.Button("▶  Validate This System", GUILayout.Height(28)))
            {
                var sysDef = (OrbitalSystemDefinition)target;
                var report = OrbitalSystemValidator.Validate(sysDef.bodies);

                // Brief result in inspector header
                string brief = report.IsValid
                    ? $"✓ Valid — {sysDef.bodies.Count} bodies, 0 errors."
                    : $"✗ {report.errors.Count} error(s), {report.warnings.Count} warning(s). See Console.";

                // Reuse ShowReport via the public API
                // (ShowReport is private — log directly here for inline brevity)
                if (report.errors.Count > 0)
                    Debug.LogError("[Validator] " + string.Join("\n", report.errors));
                if (report.warnings.Count > 0)
                    Debug.LogWarning("[Validator] " + string.Join("\n", report.warnings));

                EditorUtility.DisplayDialog("Validation", brief, "OK");
            }
        }
    }
}
#endif
