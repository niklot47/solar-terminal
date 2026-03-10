// PlanetShadowOverlay.shader
// Transparent dark overlay for the night side of a planet.
// Both star and planet world positions are set from C# — no matrix tricks needed.

Shader "SolarTerminal/PlanetShadowOverlay"
{
    Properties
    {
        _StarWorldPos       ("Star World Position",   Vector)        = (10000, 0, 0, 0)
        _PlanetWorldPos     ("Planet World Position", Vector)        = (0, 0, 0, 0)
        _NightColor         ("Night Color",           Color)         = (0, 0, 0, 1)
        _NightOpacity       ("Night Opacity",         Range(0, 1))   = 0.97
        _TerminatorSoftness ("Terminator Softness",   Range(0.001, 0.5)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+1"
        }

        Pass
        {
            Name "ShadowOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _StarWorldPos;
                float4 _PlanetWorldPos;
                float4 _NightColor;
                float  _NightOpacity;
                float  _TerminatorSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Outward direction: from planet centre to this fragment
                float3 outward = normalize(IN.positionWS - _PlanetWorldPos.xyz);

                // Direction from planet centre toward star
                float3 toStar   = _StarWorldPos.xyz - _PlanetWorldPos.xyz;
                float3 lightDir = dot(toStar, toStar) > 0.0001
                    ? normalize(toStar)
                    : float3(1, 0, 0);

                float NdotL    = dot(outward, lightDir);
                float halfSoft = _TerminatorSoftness * 0.5;
                float dayFactor = smoothstep(-halfSoft, halfSoft, NdotL);
                float alpha     = (1.0 - dayFactor) * _NightOpacity;

                return half4(_NightColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
