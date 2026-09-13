Shader "Hidden/3DGameShaders/PostProcessing"
{
    Properties
    {
        _UseBloom       ("Bloom",           Float) = 1
        _BloomThreshold ("Bloom Threshold", Range(0, 1)) = 0.6
        _BloomAmount    ("Bloom Amount",    Range(0, 2)) = 0.6
        _BlurSize       ("Blur Size",       Range(0, 4)) = 3

        _UsePixelize    ("Pixelize",        Float) = 0
        _PixelSize      ("Pixel Size",      Range(1, 32)) = 8

        _UseDilation    ("Dilation",        Float) = 0
        _DilationSize   ("Dilation Size",   Range(1, 8)) = 1
        _DilationSeparation ("Dilation Separation", Range(0, 4)) = 1

        _UseSharpen     ("Sharpen",         Float) = 0
        _SharpenAmount  ("Sharpen Amount",  Range(0, 1)) = 0.3

        _UseChromaticAberration ("Chromatic Aberration", Float) = 0
        _CAAmount       ("CA Amount",       Range(0, 3)) = 1
        _CAFocusPoint   ("CA Focus Point",  Vector) = (0.5, 0.5, 0, 0)

        _UsePosterize   ("Posterize",       Float) = 0
        _PosterizeLevels ("Posterize Levels", Range(2, 32)) = 6

        _UseFog         ("Fog",             Float) = 0
        _FogNear        ("Fog Near",        Float) = 20
        _FogFar         ("Fog Far",         Float) = 60
        _FogColor       ("Fog Color",       Color) = (0.78, 0.83, 0.9, 1)

        _UseFilmGrain   ("Film Grain",      Float) = 0
        _FilmGrainAmount ("Film Grain Amount", Range(0, 0.1)) = 0.01

        _UseSSAO        ("SSAO",            Float) = 0
        _SSAOIntensity  ("SSAO Intensity",  Range(0, 3)) = 1.0
        _SSAORadius     ("SSAO Radius",     Range(0.01, 1)) = 0.35
        _SSAOBias       ("SSAO Bias",       Range(0, 0.1)) = 0.02
        _SSAOContrast   ("SSAO Contrast",   Range(0, 2)) = 1.2
        _SSAOBlurSize   ("SSAO Blur Size",  Range(0, 4)) = 2

        _UseMotionBlur  ("Motion Blur",     Float) = 0
        _MotionBlurSamples ("Motion Blur Samples", Range(2, 16)) = 6
        _MotionBlurSeparation ("Motion Blur Separation", Range(0, 2)) = 1

        _UseOutline     ("Outline",         Float) = 0
        _OutlineDepthThreshold ("Outline Depth Threshold", Range(0.01, 2)) = 0.6
        _OutlineNormalThreshold ("Outline Normal Threshold", Range(0, 1)) = 0.8
        _OutlineColor   ("Outline Color",   Color) = (0.42, 0.36, 0.28, 1)

        _UseDOF         ("Depth Of Field",  Float) = 0
        _DOFFocusDistance ("DOF Focus Distance", Float) = 8
        _DOFRange       ("DOF Range",       Float) = 5
        _DOFBlurSize    ("DOF Blur Size",   Range(0, 16)) = 4

        _UseSSR         ("Screen Space Reflection", Float) = 0
        _SSRIntensity   ("SSR Intensity",   Range(0, 2)) = 0.35
        _SSRMaxDistance ("SSR Max Distance", Float) = 6
        _SSRThickness   ("SSR Thickness",   Range(0.01, 2)) = 0.5
        _SSRResolution  ("SSR Resolution",  Range(0.05, 1)) = 0.3
        _SSRSteps       ("SSR Binary Steps", Range(1, 10)) = 5

        _UseKuwahara    ("Painterly (Kuwahara)", Float) = 0
        _KuwaharaSize   ("Kuwahara Size",   Range(0, 5)) = 3

        _UseGamma       ("Gamma",           Float) = 0
        _Gamma          ("Gamma Value",     Range(0.2, 4)) = 1.0
        _UseLUT         ("Lookup Table",    Float) = 1
        _SunPosition    ("Sun Position",    Range(0, 1)) = 0.5
        _LUT0           ("LUT 0", 2D) = "white" {}
        _LUT1           ("LUT 1", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZTest Always
        ZWrite Off

        Pass
        {
            Name "Bright"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Bright

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BloomThreshold;

            half4 Bright(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                half value  = max(color.r, max(color.g, color.b));
                return half4(value < _BloomThreshold ? half3(0, 0, 0) : color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurSize;

            half4 Blur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int size = int(_BlurSize);
                if (size <= 0)
                {
                    return half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb, 1.0);
                }

                float2 texel = 1.0 / _ScreenParams.xy;
                half3 sum = 0;
                int count = 0;
                for (int i = -size; i <= size; ++i)
                {
                    for (int j = -size; j <= size; ++j)
                    {
                        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(i, j) * texel).rgb;
                        count += 1;
                    }
                }
                return half4(sum / count, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Combine"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Combine

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_BloomTexture);
            float _BloomAmount;

            half4 Combine(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                half3 bloom = SAMPLE_TEXTURE2D_X(_BloomTexture, sampler_LinearClamp, input.texcoord).rgb;
                return half4(color + bloom * _BloomAmount, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Pixelize"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Pixelize

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UsePixelize;
            float _PixelSize;

            half4 Pixelize(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_UsePixelize <= 0.5)
                {
                    return half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb, 1.0);
                }

                float2 fragCoord = input.texcoord * _ScreenParams.xy;
                float size = max(_PixelSize, 1.0);
                float x = floor(fragCoord.x / size) * size + size * 0.5;
                float y = floor(fragCoord.y / size) * size + size * 0.5;
                float2 uv = float2(x, y) / _ScreenParams.xy;
                return half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Dilation"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Dilation

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UseDilation;
            float _DilationSize;
            float _DilationSeparation;

            half4 Dilation(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                if (_UseDilation <= 0.5)
                {
                    return half4(center, 1.0);
                }

                int size = int(max(_DilationSize, 1.0));
                float2 texel = 1.0 / _ScreenParams.xy;
                float mx = 0.0;
                half3 cmx = center;
                for (int i = -size; i <= size; ++i)
                {
                    for (int j = -size; j <= size; ++j)
                    {
                        if (distance(float2(i, j), float2(0, 0)) > size)
                        {
                            continue;
                        }
                        half3 c = SAMPLE_TEXTURE2D_X(
                            _BlitTexture, sampler_LinearClamp,
                            input.texcoord + float2(i, j) * _DilationSeparation * texel).rgb;
                        float mxt = dot(c, half3(0.3, 0.59, 0.11));
                        if (mxt > mx)
                        {
                            mx = mxt;
                            cmx = c;
                        }
                    }
                }
                half3 color = lerp(center, cmx, smoothstep(0.2, 0.5, mx));
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Sharpen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Sharpen

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UseSharpen;
            float _SharpenAmount;

            half4 Sharpen(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_UseSharpen <= 0.5)
                {
                    return half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb, 1.0);
                }

                float2 texel = 1.0 / _ScreenParams.xy;
                float n = _SharpenAmount * -1.0;
                float m = _SharpenAmount * 4.0 + 1.0;
                half3 color =
                      SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0, 1) * texel).rgb * n
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-1, 0) * texel).rgb * n
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb * m
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(1, 0) * texel).rgb * n
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0, -1) * texel).rgb * n;
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ChromaticAberration"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ChromaticAberration

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UseChromaticAberration;
            float _CAAmount;
            float4 _CAFocusPoint;

            half4 ChromaticAberration(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_UseChromaticAberration <= 0.5)
                {
                    return half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb, 1.0);
                }

                float2 direction = input.texcoord - _CAFocusPoint.xy;
                half3 color;
                color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + direction * (0.009 * _CAAmount)).r;
                color.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + direction * (0.006 * _CAAmount)).g;
                color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + direction * (-0.006 * _CAAmount)).b;
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Posterize"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Posterize

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UsePosterize;
            float _PosterizeLevels;

            half4 Posterize(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                if (_UsePosterize <= 0.5)
                {
                    return half4(color, 1.0);
                }

                float levels = max(_PosterizeLevels, 2.0);
                float greyscale = max(color.r, max(color.g, color.b));
                float lower = floor(greyscale * levels) / levels;
                float lowerDiff = abs(greyscale - lower);
                float upper = ceil(greyscale * levels) / levels;
                float upperDiff = abs(upper - greyscale);
                float level = lowerDiff <= upperDiff ? lower : upper;
                float adjustment = greyscale > 0.0001 ? level / greyscale : 1.0;
                return half4(color * adjustment, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Fog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _UseFog;
            float _FogNear;
            float _FogFar;
            half4 _FogColor;

            half4 Fog(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                if (_UseFog <= 0.5)
                {
                    return half4(color, 1.0);
                }

                float depth = LinearEyeDepth(SampleSceneDepth(input.texcoord), _ZBufferParams);
                float near = max(_FogNear, 0.01);
                float far = max(_FogFar, near + 0.01);
                float intensity = clamp((depth - near) / (far - near), 0.0, 1.0);
                return half4(lerp(color, _FogColor.rgb, intensity), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "FilmGrain"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FilmGrain

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UseFilmGrain;
            float _FilmGrainAmount;

            half4 FilmGrain(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                if (_UseFilmGrain <= 0.5)
                {
                    return half4(color, 1.0);
                }

                float2 fragCoord = input.texcoord * _ScreenParams.xy;
                float randomIntensity = frac(
                    10000.0 * sin((fragCoord.x + fragCoord.y * _Time.y) * 0.0174532925));
                return half4(color + _FilmGrainAmount * randomIntensity, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Outline

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _UseOutline;
            float _OutlineDepthThreshold;
            float _OutlineNormalThreshold;
            half4 _OutlineColor;

            half4 Outline(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                if (_UseOutline <= 0.5)
                {
                    return half4(color, 1.0);
                }

                float2 texel = 1.0 / _ScreenParams.xy;
                float depthC = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float3 normalC = SampleSceneNormals(uv);
                float maxDepthDiff = 0.0;
                float minNormalDot = 1.0;

                for (int i = -1; i <= 1; ++i)
                {
                    for (int j = -1; j <= 1; ++j)
                    {
                        if (i == 0 && j == 0)
                        {
                            continue;
                        }
                        float2 offsetUV = uv + float2(i, j) * texel;
                        float depthN = LinearEyeDepth(SampleSceneDepth(offsetUV), _ZBufferParams);
                        maxDepthDiff = max(maxDepthDiff, abs(depthC - depthN));
                        float3 normalN = SampleSceneNormals(offsetUV);
                        minNormalDot = min(minNormalDot, dot(normalC, normalN));
                    }
                }

                float depthEdge = smoothstep(_OutlineDepthThreshold, _OutlineDepthThreshold * 1.5, maxDepthDiff);
                float normalEdge = 1.0 - smoothstep(_OutlineNormalThreshold, 1.0, minNormalDot);
                float edge = saturate(max(depthEdge, normalEdge));
                half3 outlineColor = color * _OutlineColor.rgb;
                return half4(lerp(color, outlineColor, edge), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SSAO"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment SSAO

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _UseSSAO;
            float _SSAORadius;
            float _SSAOBias;
            float _SSAOIntensity;
            float _SSAOContrast;

            half4 SSAO(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                if (_UseSSAO <= 0.5)
                {
                    return half4(1.0, 1.0, 1.0, 1.0);
                }

                float rawDepth = SampleSceneDepth(uv);
                float3 positionWS = ComputeWorldSpacePosition(uv * 2.0 - 1.0, rawDepth, UNITY_MATRIX_I_VP);
                float3 normalWS = normalize(SampleSceneNormals(uv));

                // Tiled pseudo-random rotation (4x4 tiles, like a small noise texture).
                float2 noiseUV = floor(uv * _ScreenParams.xy / 4.0);
                float angle = frac(sin(dot(noiseUV, float2(12.9898, 78.233))) * 43758.5453) * 6.28318530718;
                float3 random = float3(cos(angle), sin(angle), 0.0);

                float3 tangent = normalize(random - normalWS * dot(random, normalWS));
                float3 bitangent = cross(normalWS, tangent);

                static const float3 kernel[8] =
                {
                    float3(0.35, 0.20, 0.91),
                    float3(-0.30, 0.35, 0.89),
                    float3(0.25, -0.40, 0.88),
                    float3(-0.40, -0.25, 0.88),
                    float3(0.80, 0.05, 0.60),
                    float3(-0.05, 0.80, 0.60),
                    float3(0.70, -0.60, 0.39),
                    float3(-0.70, -0.55, 0.45)
                };

                float occlusion = 0.0;
                for (int i = 0; i < 8; ++i)
                {
                    float3 dir = normalize(kernel[i]);
                    float3 samplePos = positionWS
                        + (tangent * dir.x + bitangent * dir.y + normalWS * dir.z) * _SSAORadius;

                    float4 clip = TransformWorldToHClip(float4(samplePos, 1.0));
                    float2 sampleUV = clip.xy / clip.w * 0.5 + 0.5;
                    if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
                    {
                        continue;
                    }

                    float occDepth = SampleSceneDepth(sampleUV);
                    float3 occWS = ComputeWorldSpacePosition(sampleUV * 2.0 - 1.0, occDepth, UNITY_MATRIX_I_VP);

                    float sampleViewZ = -TransformWorldToView(samplePos).z;
                    float occViewZ = -TransformWorldToView(occWS).z;

                    float occluded = (occViewZ < sampleViewZ - _SSAOBias) ? 1.0 : 0.0;
                    float rangeCheck = smoothstep(
                        0.0, 1.0,
                        _SSAORadius / max(abs(sampleViewZ - occViewZ), 0.0001));
                    occlusion += occluded * rangeCheck;
                }

                occlusion /= 8.0;
                occlusion = 1.0 - occlusion;
                occlusion = pow(max(occlusion, 0.0), _SSAOIntensity);
                occlusion = _SSAOContrast * (occlusion - 0.5) + 0.5;
                occlusion = saturate(occlusion);
                return half4(occlusion, occlusion, occlusion, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SSAOApply"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment SSAOApply

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_SSAOTexture);

            half4 SSAOApply(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                half ao = SAMPLE_TEXTURE2D_X(_SSAOTexture, sampler_LinearClamp, input.texcoord).r;
                return half4(color * ao, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "MotionBlur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment MotionBlur

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_MotionVectorTexture);
            float _UseMotionBlur;
            float _MotionBlurSamples;
            float _MotionBlurSeparation;

            half4 MotionBlur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                if (_UseMotionBlur <= 0.5)
                {
                    return half4(color, 1.0);
                }

                // Motion vectors are stored as an NDC-space delta; convert to UV space.
                float2 direction = SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_PointClamp, uv).rg * 0.5;
                if (length(direction) <= 0.0001)
                {
                    return half4(color, 1.0);
                }

                int samples = int(max(_MotionBlurSamples, 1.0));
                direction *= _MotionBlurSeparation;
                float2 forward = uv;
                float2 backward = uv;
                half3 acc = color;
                float count = 1.0;
                for (int i = 0; i < samples; ++i)
                {
                    forward += direction;
                    backward -= direction;
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, forward).rgb;
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, backward).rgb;
                    count += 2.0;
                }
                return half4(acc / count, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DOFMix"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DOFMix

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_DOFBlurTexture);
            float _UseDOF;
            float _DOFFocusDistance;
            float _DOFRange;

            half4 DOFMix(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 sharp = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                if (_UseDOF <= 0.5)
                {
                    return half4(sharp, 1.0);
                }

                half3 blur = SAMPLE_TEXTURE2D_X(_DOFBlurTexture, sampler_LinearClamp, uv).rgb;
                float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float range = max(_DOFRange, 0.01);
                float amount = smoothstep(range * 0.5, range, abs(depth - _DOFFocusDistance));
                return half4(lerp(sharp, blur, saturate(amount)), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ScreenSpaceReflection"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ScreenSpaceReflection

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _UseSSR;
            float _SSRIntensity;
            float _SSRMaxDistance;
            float _SSRThickness;
            float _SSRResolution;
            float _SSRSteps;

            float2 ProjectToUv(float4 clip)
            {
                float2 ndc = clip.xy / clip.w;
                float2 uvYUp = ndc * 0.5 + 0.5;
                return float2(uvYUp.x, 1.0 - uvYUp.y);
            }

            half4 ScreenSpaceReflection(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                if (_UseSSR <= 0.5)
                {
                    return half4(color, 1.0);
                }

                float rawDepth = SampleSceneDepth(uv);
                float2 ndcUV = float2(uv.x, 1.0 - uv.y);
                float3 positionWS = ComputeWorldSpacePosition(ndcUV, rawDepth, UNITY_MATRIX_I_VP);
                float3 normalWS = normalize(SampleSceneNormals(uv));
                float3 viewPos = TransformWorldToView(positionWS);
                float3 viewNormal = TransformWorldToViewDir(normalWS);
                float3 unitViewDir = normalize(-viewPos);
                float3 pivot = normalize(reflect(unitViewDir, viewNormal));

                float maxDistance = max(_SSRMaxDistance, 0.01);
                float startEye = -viewPos.z;
                float3 endViewPos = viewPos + pivot * maxDistance;
                float endEye = -endViewPos.z;

                float2 startPixel = ProjectToUv(mul(UNITY_MATRIX_P, float4(viewPos, 1.0))) * _ScreenParams.xy;
                float2 endPixel = ProjectToUv(mul(UNITY_MATRIX_P, float4(endViewPos, 1.0))) * _ScreenParams.xy;

                float2 deltaPixel = endPixel - startPixel;
                float useX = abs(deltaPixel.x) >= abs(deltaPixel.y) ? 1.0 : 0.0;
                float delta = lerp(abs(deltaPixel.y), abs(deltaPixel.x), useX) * clamp(_SSRResolution, 0.01, 1.0);
                float2 increment = deltaPixel / max(delta, 0.001);
                float2 fragPixel = startPixel;
                float search0 = 0.0;
                float search1 = 0.0;
                int hit0 = 0;
                int hit1 = 0;
                float thickness = max(_SSRThickness, 0.01);
                float depthDiff = thickness;

                for (int i = 0; i < int(delta); ++i)
                {
                    fragPixel += increment;
                    float2 sampleUV = fragPixel / _ScreenParams.xy;
                    if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
                    {
                        continue;
                    }
                    search1 = useX * ((fragPixel.x - startPixel.x) / deltaPixel.x)
                        + (1.0 - useX) * ((fragPixel.y - startPixel.y) / deltaPixel.y);
                    search1 = clamp(search1, 0.0, 1.0);
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                    float rayEye = lerp(startEye, endEye, search1);
                    depthDiff = rayEye - sceneEye;
                    if (depthDiff > 0.0 && depthDiff < thickness)
                    {
                        hit0 = 1;
                        break;
                    }
                    search0 = search1;
                }

                search1 = search0 + (search1 - search0) * 0.5;
                int steps = int(max(_SSRSteps, 1.0)) * hit0;
                for (int i = 0; i < steps; ++i)
                {
                    float2 sampleUV = lerp(startPixel, endPixel, search1) / _ScreenParams.xy;
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                    float rayEye = lerp(startEye, endEye, search1);
                    depthDiff = rayEye - sceneEye;
                    if (depthDiff > 0.0 && depthDiff < thickness)
                    {
                        hit1 = 1;
                        search1 = search0 + (search1 - search0) * 0.5;
                    }
                    else
                    {
                        float temp = search1;
                        search1 += (search1 - search0) * 0.5;
                        search0 = temp;
                    }
                }

                float2 hitUV = lerp(startPixel, endPixel, search1) / _ScreenParams.xy;
                float3 hitViewPos = lerp(viewPos, endViewPos, search1);
                float visibility = hit1
                    * (1.0 - max(dot(-unitViewDir, pivot), 0.0))
                    * (1.0 - clamp(depthDiff / thickness, 0.0, 1.0))
                    * (1.0 - clamp(length(hitViewPos - viewPos) / maxDistance, 0.0, 1.0));
                visibility = saturate(visibility);
                if (visibility <= 0.001
                    || hitUV.x < 0.0 || hitUV.x > 1.0
                    || hitUV.y < 0.0 || hitUV.y > 1.0)
                {
                    return half4(color, 1.0);
                }

                half3 reflection = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, hitUV).rgb;
                half3 result = lerp(color, reflection, visibility * _SSRIntensity);
                return half4(result, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Final"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Final

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UseGamma;
            float _Gamma;
            float _UseLUT;
            float _SunPosition;
            TEXTURE2D(_LUT0); SAMPLER(sampler_LUT0);
            TEXTURE2D(_LUT1); SAMPLER(sampler_LUT1);

            half3 ApplyLUT(half3 color)
            {
                float r = clamp(color.r, 0.0, 1.0);
                float g = clamp(color.g, 0.0, 1.0);
                float b = clamp(color.b, 0.0, 1.0);

                float u0 = (floor(r * 15.0) / 15.0 * 15.0 + floor(b * 15.0) / 15.0 * 240.0) / 255.0;
                float v0 = 1.0 - floor(g * 15.0) / 15.0;
                float u1 = (ceil(r * 15.0) / 15.0 * 15.0 + ceil(b * 15.0) / 15.0 * 240.0) / 255.0;
                float v1 = 1.0 - ceil(g * 15.0) / 15.0;

                half3 left0  = SAMPLE_TEXTURE2D(_LUT0, sampler_LUT0, float2(u0, v0)).rgb;
                half3 left1  = SAMPLE_TEXTURE2D(_LUT1, sampler_LUT1, float2(u0, v0)).rgb;
                half3 right0 = SAMPLE_TEXTURE2D(_LUT0, sampler_LUT0, float2(u1, v1)).rgb;
                half3 right1 = SAMPLE_TEXTURE2D(_LUT1, sampler_LUT1, float2(u1, v1)).rgb;

                half3 left  = lerp(left0,  left1,  _SunPosition);
                half3 right = lerp(right0, right1, _SunPosition);

                half3 graded;
                graded.r = lerp(left.r, right.r, frac(r * 15.0));
                graded.g = lerp(left.g, right.g, frac(g * 15.0));
                graded.b = lerp(left.b, right.b, frac(b * 15.0));
                return graded;
            }

            half4 Final(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;

                if (_UseGamma > 0.5)
                {
                    color = pow(max(color, half3(0, 0, 0)), half3(_Gamma, _Gamma, _Gamma));
                }

                if (_UseLUT > 0.5)
                {
                    // lookup-table.frag converts to sRGB, samples the tables and
                    // converts back, so the lookup runs on sRGB values rather
                    // than on the linear buffer.
                    half3 srgb = pow(max(color, half3(0, 0, 0)), half3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2));
                    srgb = ApplyLUT(srgb);
                    color = pow(max(srgb, half3(0, 0, 0)), half3(2.2, 2.2, 2.2));
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Kuwahara"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Kuwahara
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _UseKuwahara;
            float _KuwaharaSize;

            static const half3 KuwaharaLuma = half3(0.3, 0.59, 0.11);

            // Mean colour and luminance variance over one quadrant of the kernel.
            // kuwahara-filter.frag picks whichever quadrant is flattest, which is
            // what produces the painterly look: flat areas keep their colour and
            // edges snap to one side instead of blurring across.
            void KuwaharaQuadrant(
                float2 uv, float2 texel, int2 from, int2 to,
                out half3 meanColor, out float variance)
            {
                half3 sum = 0;
                float sumSquared = 0;
                float count = 0;

                for (int i = from.x; i <= to.x; ++i)
                {
                    for (int j = from.y; j <= to.y; ++j)
                    {
                        half3 c = SAMPLE_TEXTURE2D_X(
                            _BlitTexture, sampler_LinearClamp,
                            uv + float2(i, j) * texel).rgb;
                        float l = dot(c, KuwaharaLuma);
                        sum += c;
                        sumSquared += l * l;
                        count += 1.0;
                    }
                }

                meanColor = sum / max(count, 1.0);
                float mean = dot(meanColor, KuwaharaLuma);
                variance = max(sumSquared / max(count, 1.0) - mean * mean, 0.0);
            }

            half4 Kuwahara(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                int size = (int)clamp(_KuwaharaSize, 0.0, 5.0);
                if (_UseKuwahara <= 0.5 || size <= 0)
                {
                    return half4(center, 1.0);
                }

                float2 texel = 1.0 / _ScreenParams.xy;

                half3 mean;
                float variance;
                half3 best = center;
                float bestVariance = -1.0;

                // Lower left, upper right, upper left, lower right.
                KuwaharaQuadrant(uv, texel, int2(-size, -size), int2(0, 0), mean, variance);
                if (bestVariance < 0.0 || variance < bestVariance)
                {
                    best = mean;
                    bestVariance = variance;
                }

                KuwaharaQuadrant(uv, texel, int2(0, 0), int2(size, size), mean, variance);
                if (variance < bestVariance)
                {
                    best = mean;
                    bestVariance = variance;
                }

                KuwaharaQuadrant(uv, texel, int2(-size, 0), int2(0, size), mean, variance);
                if (variance < bestVariance)
                {
                    best = mean;
                    bestVariance = variance;
                }

                KuwaharaQuadrant(uv, texel, int2(0, -size), int2(size, 0), mean, variance);
                if (variance < bestVariance)
                {
                    best = mean;
                }

                return half4(best, 1.0);
            }
            ENDHLSL
        }
    }
}
