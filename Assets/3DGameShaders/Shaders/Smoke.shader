// Unlit, alpha blended particle shader with soft particle fading.
//
// The original draws its smoke with a sprite particle renderer and no depth
// interaction, so a plume that passes through the mill roof cuts a hard line
// across it. Fading the sprite out where the scene depth is closer than the
// sprite removes that seam.
Shader "3DGameShaders/Smoke"
{
    Properties
    {
        _MainTex  ("Smoke", 2D) = "white" {}
        _Color    ("Tint", Color) = (1, 1, 1, 1)
        _SoftFade ("Soft Particle Fade", Range(0.01, 4)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SmokeForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _SoftFade;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float4 color      : COLOR;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positionInputs.positionCS;
                output.screenPos  = positionInputs.positionNDC;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.color      = input.color;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv)
                            * input.color * _Color;

                // Soft particle fade: nothing to fade against if the depth
                // texture is missing, so the term is 1 in that case.
                float2 screenUV   = input.screenPos.xy / input.screenPos.w;
                float  sceneEye   = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float  particleEye = -TransformWorldToView(input.positionWS).z;
                color.a *= saturate((sceneEye - particleEye) / max(_SoftFade, 0.01));

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
