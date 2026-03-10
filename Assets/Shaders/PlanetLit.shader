// PlanetLit.shader
// URP planetary lighting shader — uses star WORLD POSITION (not direction).
// Each planet computes its own light direction as normalize(starPos - fragmentPos),
// so all planets are lit correctly regardless of their orbital position.
// SRP Batcher compatible.

Shader "SolarTerminal/PlanetLit"
{
    Properties
    {
        [MainTexture]
        _BaseMap            ("Albedo (RGB)", 2D)                = "white" {}

        [MainColor]
        _BaseColor          ("Base Color Tint", Color)          = (1, 1, 1, 1)

        [Normal]
        _NormalMap          ("Normal Map", 2D)                  = "bump" {}
        _NormalStrength     ("Normal Strength", Range(0, 2))    = 1.0

        // Set by StarLightDirector every frame.
        // Default is far along +X so the material preview is readable.
        _StarWorldPos       ("Star World Position", Vector)     = (10000, 0, 0, 0)

        _LightIntensity     ("Light Intensity",     Range(0, 4))     = 1.2
        _NightBrightness    ("Night Brightness",    Range(0, 0.2))   = 0.01
        _TerminatorSoftness ("Terminator Softness", Range(0.001, 0.5)) = 0.05
        _AmbientSpaceLight  ("Ambient Space Light", Range(0, 0.1))   = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _StarWorldPos;
                float  _NormalStrength;
                float  _LightIntensity;
                float  _NightBrightness;
                float  _TerminatorSoftness;
                float  _AmbientSpaceLight;
            CBUFFER_END

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 positionWS  : TEXCOORD4;   // fragment world pos for light dir calc
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS    = nrmInputs.normalWS;
                OUT.tangentWS   = nrmInputs.tangentWS;
                OUT.bitangentWS = nrmInputs.bitangentWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Albedo
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Normal map
                half3 normalTS = UnpackNormal(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                normalTS.xy *= _NormalStrength;
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS));
                float3 normalWS = normalize(mul(normalTS, TBN));

                // Light direction = from this fragment toward the star.
                // Each planet gets its own correct direction regardless of orbit position.
                // Safe fallback to +X if star position was never set (e.g. in material preview).
                float3 toStar   = _StarWorldPos.xyz - IN.positionWS;
                float3 lightDir = dot(toStar, toStar) > 0.0001
                    ? normalize(toStar)
                    : float3(1, 0, 0);

                // NdotL: +1 = facing star, -1 = facing away
                float NdotL = dot(normalWS, lightDir);

                // Soft terminator
                float halfSoft  = _TerminatorSoftness * 0.5;
                float dayFactor = smoothstep(-halfSoft, halfSoft, NdotL);

                // Day / night colours
                float3 dayColor   = albedo.rgb * _LightIntensity;
                float3 nightColor = albedo.rgb * _NightBrightness;
                float3 ambient    = albedo.rgb * _AmbientSpaceLight;

                float3 finalColor = lerp(nightColor, dayColor, dayFactor) + ambient;
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _StarWorldPos;
                float  _NormalStrength;
                float  _LightIntensity;
                float  _NightBrightness;
                float  _TerminatorSoftness;
                float  _AmbientSpaceLight;
            CBUFFER_END
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
