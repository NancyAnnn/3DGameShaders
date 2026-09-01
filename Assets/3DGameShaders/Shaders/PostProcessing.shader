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
                    color = ApplyLUT(color);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}