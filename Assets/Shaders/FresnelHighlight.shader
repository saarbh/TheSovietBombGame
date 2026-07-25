Shader "TheHotWar/FresnelHighlight"
{
    // Additive Fresnel rim, designed to be added as an extra material on an object
    // so it overlays a view-facing glow without replacing the base material.
    // Drive "_Highlight" (0..1) from HoverHighlight to fade the rim on hover.
    Properties
    {
        [HDR] _OutlineColor ("Outline Color (HDR)", Color) = (0, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.25, 8)) = 3
        _FresnelStrength ("Fresnel Strength", Range(0, 4)) = 1.5
        _Highlight ("Highlight (0-1)", Range(0, 1)) = 0
    }

    SubShader
    {
        // Transparent + late queue so the rim draws over the opaque surface it hugs.
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FresnelHighlight"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One      // additive
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _FresnelPower;
                half _FresnelStrength;
                half _Highlight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = positionInputs.positionCS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                // 1.0 at grazing edges, 0.0 facing the camera - the classic Fresnel rim.
                half facing = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(saturate(1.0h - facing), _FresnelPower);

                half3 rim = _OutlineColor.rgb * fresnel * _FresnelStrength * _Highlight;
                return half4(rim, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
