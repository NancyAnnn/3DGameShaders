// Screen space water for the Unity URP port of lettier/3d-game-shaders-for-beginners.
//
// Ported from the original GLSL (BSD 3-Clause):
//   normal.frag, base.frag, screen-space-refraction.frag, refraction.frag,
//   screen-space-reflection.frag, reflection-color.frag, foam.frag, foam-mask.frag,
//   base-combine.frag
// (C) 2019 David Lettier - lettier.com
//
// The original composites the water in screen space out of G-buffer layers and
// combines them in this order (base-combine.frag):
//
//     color  = refraction                       // background + depth tint
//     color  = mix(color, reflection, reflA)    // reflection.a = visibility * mask.r
//     color  = mix(color, foam,       foamA)
//     color += specular * specular.a
//
// This shader reproduces that order on the water mesh itself. The screen space
// inputs the G-buffer used to supply come from the camera textures instead:
//   * scene depth   -> water thickness, foam, and the SSR march
//   * opaque colour -> refraction and reflection taps
//
// Prerequisite: the camera must have BOTH the depth texture and the opaque
// texture enabled, otherwise every screen space tap reads an unbound texture.
// (The post processing pass cannot supply the depth in time - its copy is
// scheduled after the transparent queue, which is where this shader runs.)

Shader "3DGameShaders/WaterSurface"
{
    Properties
    {
        [Header(Flow Mapped Normal)]
        _NormalMap          ("Normal Map", 2D) = "bump" {}
        _FlowMap            ("Flow Map", 2D) = "gray" {}
        // normal.frag scrolls by "flow * frameTime" with no speed multiplier, so
        // 1.0 here is the tutorial's rate: the flow map's own vector per second.
        // up-flow.png carries (0, 0.25), i.e. a quarter UV per second.
        _FlowSpeed          ("Flow Speed", Float) = 1
        _NormalTiling       ("Normal Tiling", Float) = 1
        _NormalStrength     ("Normal Strength", Range(0, 2)) = 1

        [Header(Screen Space Refraction)]
        // The water owns a blue diffuse map; that blue is the body colour of the
        // river. The tutorial's flat tint only pulls it slightly.
        _DiffuseMap         ("Diffuse Map", 2D) = "white" {}
        _AmbientStrength    ("Ambient Strength", Range(0, 2)) = 0.8
        _TintColor          ("Deep Water Tint", Color) = (0.392, 0.537, 0.561, 1)
        _TintStrength       ("Tint Strength", Range(0, 1)) = 0.15
        _WaterDepth         ("Water Depth Max", Float) = 2
        // How far the deep water colour may take over from the river bed. At 1
        // the bed is gone; at 0.8 the bed still shows through the water.
        _WaterBodyStrength  ("Water Body Strength", Range(0, 1)) = 0.8
        _RefractionStrength ("Refraction Offset (px)", Range(0, 64)) = 24

        [Header(Screen Space Reflection)]
        _UseSSR             ("Use SSR", Float) = 1
        _SSRMaxDistance     ("SSR Max Distance", Float) = 8
        _SSRResolution      ("SSR Resolution", Range(0.05, 1)) = 0.3
        _SSRSteps           ("SSR Binary Steps", Range(1, 8)) = 5
        _SSRThickness       ("SSR Thickness", Range(0.01, 5)) = 0.5
        _ReflectionAmount   ("Reflection Amount", Range(0, 1)) = 0.8
        _ReflectionRoughness("Reflection Roughness", Range(0, 1)) = 0.5
        // Screen space reflection cannot see the sky - there is no depth for it -
        // so the environment reflection covers everything the march misses.
        _EnvironmentStrength("Environment Reflection", Range(0, 2)) = 0.8

        [Header(Foam)]
        _FoamPattern        ("Foam Pattern", 2D) = "white" {}
        _FoamColor          ("Foam Color", Color) = (0.8, 0.85, 0.92, 1)
        // Foam only forms where the water is shallower than this. The mill's
        // river bed is a flat plane ~3 units below the surface, so foam shows up
        // where geometry crosses the surface (water wheel, dock). Raise it to
        // push foam further out into the open water.
        _FoamDepth          ("Foam Depth Max", Float) = 1.5
        _FoamTiling         ("Foam Tiling", Float) = 1
        _FoamIntensity      ("Foam Intensity", Range(0, 1)) = 1
        // The raw foam pattern averages ~0.26 and reads as a flat haze, so it is
        // thresholded into distinct patches.
        _FoamThreshold      ("Foam Threshold", Range(0, 1)) = 0.35
        _FoamSoftness       ("Foam Softness", Range(0.01, 1)) = 0.2
        // foam.frag reshapes its depth term with an ease-in/out curve that
        // collapses to x^2 near the shore, so the foam survives only a hairline
        // at the waterline. A gentler power keeps the band wide enough to read.
        _FoamFalloff        ("Foam Falloff", Range(0.2, 3)) = 1.2

        // 0 = shaded water, 1 = foam, 2 = depth factor, 3 = reflection, 4 = thickness.
        // Only used by the render probe.
        _DebugView          ("Debug View", Range(0, 4)) = 0

        [Header(Specular)]
        _SpecularMap        ("Specular Map", 2D) = "white" {}
        _SpecularIntensity  ("Specular Intensity", Range(0, 4)) = 1

        // The mill geometry is parsed straight out of the OBJ, so the winding is
        // not guaranteed. The other mill materials render two sided for the same
        // reason; default to that here as well.
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
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
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            // No depth write: the depth buffer has to keep holding the river bed
            // so the thickness, the foam and the SSR march can read it.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _AmbientStrength;
                float  _FlowSpeed;
                float  _NormalTiling;
                float  _NormalStrength;
                float4 _TintColor;
                float  _TintStrength;
                float  _WaterDepth;
                float  _WaterBodyStrength;
                float  _RefractionStrength;
                float  _UseSSR;
                float  _SSRMaxDistance;
                float  _SSRResolution;
                float  _SSRSteps;
                float  _SSRThickness;
                float  _ReflectionAmount;
                float  _ReflectionRoughness;
                float  _EnvironmentStrength;
                float4 _FoamColor;
                float  _FoamDepth;
                float  _FoamTiling;
                float  _FoamIntensity;
                float  _FoamThreshold;
                float  _FoamSoftness;
                float  _FoamFalloff;
                float  _DebugView;
                float  _SpecularIntensity;
                float  _Cull;
            CBUFFER_END

            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FlowMap);     SAMPLER(sampler_FlowMap);
            TEXTURE2D(_FoamPattern); SAMPLER(sampler_FoamPattern);
            TEXTURE2D(_SpecularMap); SAMPLER(sampler_SpecularMap);
            TEXTURE2D(_DiffuseMap);  SAMPLER(sampler_DiffuseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                float3 viewDirWS  : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normalInputs   = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.screenPos  = positionInputs.positionNDC;
                output.uv         = input.uv;
                output.positionWS = positionInputs.positionWS;
                output.normalWS   = normalInputs.normalWS;
                output.viewDirWS  = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            // Mirrors GetVertexPositionInputs().positionNDC, so a projected point
            // lands on the same screen uv convention the depth/colour use.
            float2 ProjectToScreenUV(float3 positionWS)
            {
                float4 positionCS = TransformWorldToHClip(positionWS);
                float4 ndc        = positionCS * 0.5;
                float2 screenPos  = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
                return screenPos / positionCS.w;
            }

            float SampleEyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            // The march loop is divergent, so implicit derivatives (and with them
            // the mip choice) are undefined inside it. Explicit LOD 0 instead.
            float SampleEyeDepthLod0(float2 uv)
            {
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(
                    _CameraDepthTexture, sampler_CameraDepthTexture,
                    UnityStereoTransformScreenSpaceTex(uv), 0).r;
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half3 SampleSceneColorLod0(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(
                    _CameraOpaqueTexture, sampler_CameraOpaqueTexture,
                    UnityStereoTransformScreenSpaceTex(uv), 0).rgb;
            }

            // reflection-color.frag mixes the sharp reflection with a blurred copy
            // by the roughness in the water mask (green channel = 0.5).
            half3 SampleBlurredSceneColor(float2 uv, float blurPixels)
            {
                half3 color = SampleSceneColorLod0(uv);
                if (blurPixels > 0.01)
                {
                    float2 texel = blurPixels / _ScreenParams.xy;
                    half3 blurred = SampleSceneColorLod0(uv + float2( texel.x,  texel.y)) * 0.25;
                    blurred      += SampleSceneColorLod0(uv + float2(-texel.x,  texel.y)) * 0.25;
                    blurred      += SampleSceneColorLod0(uv + float2( texel.x, -texel.y)) * 0.25;
                    blurred      += SampleSceneColorLod0(uv + float2(-texel.x, -texel.y)) * 0.25;
                    color = lerp(color, blurred, saturate(_ReflectionRoughness));
                }
                return color;
            }

            // Screen space reflection - port of screen-space-reflection.frag. The
            // tutorial marches view space points against a position buffer; here
            // the same march runs against the camera depth texture.
            half3 SampleScreenSpaceReflection(
                float3 positionWS,
                float3 rayDirWS,
                float3 viewDirWS,
                out float visibility)
            {
                visibility = 0.0;

                float  maxDistance = max(_SSRMaxDistance, 0.01);
                float3 rayEndWS    = positionWS + rayDirWS * maxDistance;

                float2 startPixel = ProjectToScreenUV(positionWS) * _ScreenParams.xy;
                float2 endPixel   = ProjectToScreenUV(rayEndWS) * _ScreenParams.xy;
                float2 deltaPixel = endPixel - startPixel;

                float  useX      = abs(deltaPixel.x) >= abs(deltaPixel.y) ? 1.0 : 0.0;
                float  delta     = lerp(abs(deltaPixel.y), abs(deltaPixel.x), useX);
                delta            = delta * clamp(_SSRResolution, 0.05, 1.0);
                float2 increment = deltaPixel / max(delta, 0.001);

                float  thickness = max(_SSRThickness, 0.01);
                float  depthDiff = thickness;
                float2 fragPixel = startPixel;
                float  search0   = 0.0;
                float  search1   = 0.0;
                float  hit       = 0.0;
                float2 hitUV     = 0.0;

                int marchSteps = (int)min(delta, 128.0);
                for (int i = 0; i < marchSteps; ++i)
                {
                    fragPixel += increment;
                    float2 sampleUV = fragPixel / _ScreenParams.xy;
                    if (sampleUV.x < 0.0 || sampleUV.x > 1.0 ||
                        sampleUV.y < 0.0 || sampleUV.y > 1.0)
                    {
                        continue;
                    }

                    search1 = lerp((fragPixel.y - startPixel.y) / deltaPixel.y,
                                   (fragPixel.x - startPixel.x) / deltaPixel.x,
                                   useX);
                    search1 = saturate(search1);

                    float3 rayPos   = lerp(positionWS, rayEndWS, search1);
                    float  rayEye   = -TransformWorldToView(rayPos).z;
                    float  sceneEye = SampleEyeDepthLod0(sampleUV);

                    depthDiff = rayEye - sceneEye;
                    if (depthDiff > 0.0 && depthDiff < thickness)
                    {
                        hit    = 1.0;
                        hitUV  = sampleUV;
                        break;
                    }
                    search0 = search1;
                }

                // Bisection on the [last miss, first hit] bracket.
                float lo = search0;
                float hi = search1;
                int refineSteps = hit > 0.5 ? (int)clamp(_SSRSteps, 1.0, 8.0) : 0;
                for (int j = 0; j < refineSteps; ++j)
                {
                    float  mid      = 0.5 * (lo + hi);
                    float2 sampleUV = lerp(startPixel, endPixel, mid) / _ScreenParams.xy;
                    float3 rayPos   = lerp(positionWS, rayEndWS, mid);
                    float  rayEye   = -TransformWorldToView(rayPos).z;
                    float  sceneEye = SampleEyeDepthLod0(sampleUV);
                    float  diff     = rayEye - sceneEye;

                    if (diff > 0.0 && diff < thickness)
                    {
                        hi        = mid;
                        hitUV     = sampleUV;
                        depthDiff = diff;
                    }
                    else
                    {
                        lo = mid;
                    }
                }

                if (hit <= 0.5 ||
                    hitUV.x <= 0.0 || hitUV.x >= 1.0 ||
                    hitUV.y <= 0.0 || hitUV.y >= 1.0)
                {
                    return 0.0;
                }

                float3 hitPos = lerp(positionWS, rayEndWS, hi);
                visibility = saturate(
                      hit
                    * (1.0 - max(dot(-viewDirWS, rayDirWS), 0.0))
                    * (1.0 - saturate(depthDiff / thickness))
                    * (1.0 - saturate(length(hitPos - positionWS) / maxDistance))
                    * _ReflectionAmount);

                return SampleBlurredSceneColor(hitUV, _ReflectionRoughness * 8.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV   = input.screenPos.xy / input.screenPos.w;
                float  surfaceEye = -TransformWorldToView(input.positionWS).z;
                float  time       = _Time.y;

                // ---------------------------------------------------------
                // Flow mapped normal - normal.frag scrolls the normal map along
                // the direction stored in the flow map.
                // ---------------------------------------------------------
                float2 flow = (SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, input.uv).rg - 0.5) * 2.0;
                flow.x = abs(flow.x) <= 0.02 ? 0.0 : flow.x;
                flow.y = abs(flow.y) <= 0.02 ? 0.0 : flow.y;

                float2 normalUV = input.uv * _NormalTiling + flow * (_FlowSpeed * time);
                half3  normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV));
                normalTS.xy *= _NormalStrength;
                normalTS     = normalize(normalTS);

                // The parsed OBJ mesh carries no tangents, so the tangent frame is
                // rebuilt from the surface normal.
                float3 meshNormalWS = normalize(input.normalWS);
                float3 up        = abs(meshNormalWS.y) < 0.999 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 tangentWS = normalize(cross(up, meshNormalWS));
                float3 binormalWS = cross(meshNormalWS, tangentWS);
                float3 normalWS  = normalize(
                    tangentWS * normalTS.x + binormalWS * normalTS.y + meshNormalWS * normalTS.z);
                float3 viewDirWS = normalize(input.viewDirWS);

                // ---------------------------------------------------------
                // Water thickness: everything below is driven by how much scene
                // sits behind the surface at this pixel.
                // ---------------------------------------------------------
                float sceneEye    = SampleEyeDepth(screenUV);
                float thickness   = max(sceneEye - surfaceEye, 0.0);
                // Exponential absorption rather than a clamped ratio: the mill's
                // river is ~3 units deep everywhere, so a clamped ratio pins the
                // result at "fully deep" and the bed disappears completely.
                float depth01     = 1.0 - exp(-thickness / max(_WaterDepth, 0.0001));

                // The water body colour: the river's own blue diffuse map, lit the
                // same way BaseLit lights everything else. Without this the water
                // is only a grey tint over the river bed, because the forward
                // opaque texture it refracts does not contain the water itself.
                Light mainLight = GetMainLight();
                half3 albedo    = SAMPLE_TEXTURE2D(_DiffuseMap, sampler_DiffuseMap, input.uv).rgb;
                float ndl       = saturate(dot(normalWS, mainLight.direction));
                float up01      = normalWS.y * 0.5 + 0.5;
                half3 skyLight  = lerp(half3(0.302, 0.451, 0.471),
                                       half3(0.765, 0.573, 0.400), up01);
                half3 bodyColor = albedo * (mainLight.color * ndl + skyLight * _AmbientStrength);

                // ---------------------------------------------------------
                // 1. Screen space refraction (refraction.frag).
                //    The background is pushed around by the water normal, then
                //    tinted towards the deep colour by the water depth.
                // ---------------------------------------------------------
                float3 viewNormal  = TransformWorldToViewDir(normalWS);
                float  offsetPixels = _RefractionStrength * lerp(0.35, 1.0, depth01);
                float2 refractedUV  = screenUV + viewNormal.xy * offsetPixels / _ScreenParams.xy;
                refractedUV = clamp(refractedUV, 0.001, 0.999);

                // If the offset tap lands on geometry in front of the surface the
                // background would smear into the water, so fall back to the
                // unrefracted tap for this pixel.
                if (SampleEyeDepth(refractedUV) < surfaceEye)
                {
                    refractedUV = screenUV;
                }

                half3 background = SampleSceneColor(refractedUV);
                half3 deepColor  = lerp(bodyColor, _TintColor.rgb, _TintStrength);
                half3 color      = lerp(background, deepColor, depth01 * _WaterBodyStrength);

                // ---------------------------------------------------------
                // 2. Screen space reflection.
                // ---------------------------------------------------------
                float3 reflectionDirWS = reflect(-viewDirWS, normalWS);
                float  grazing         = saturate(1.0 - dot(viewDirWS, normalWS));

                // Environment reflection first. URP feeds ReflectionProbe's
                // default texture into GlossyEnvironmentReflection every frame,
                // and that default reflection is built from the skybox - so this
                // is what a flat water surface actually reflects. Screen space
                // reflection can never supply the sky: there is no depth for it.
                half3 reflectionColor = GlossyEnvironmentReflection(
                    reflectionDirWS, _ReflectionRoughness, 1.0);
                float reflectionAlpha = _EnvironmentStrength * grazing;

                // Screen space reflection overrides it wherever the depth buffer
                // had geometry to hit (the mill, the trees, the banks).
                if (_UseSSR > 0.5)
                {
                    float ssrVisibility = 0.0;
                    half3 ssrColor = SampleScreenSpaceReflection(
                        input.positionWS, reflectionDirWS, viewDirWS, ssrVisibility);
                    reflectionColor = lerp(reflectionColor, ssrColor, ssrVisibility);
                    reflectionAlpha = max(reflectionAlpha,
                                          ssrVisibility * _ReflectionAmount);
                }

                color = lerp(color, reflectionColor, saturate(reflectionAlpha));

                // ---------------------------------------------------------
                // 3. Foam (foam.frag + foam-mask.frag). Shallow water near the
                //    shore lifts the flow animated foam pattern.
                // ---------------------------------------------------------
                float2 foamUV = input.uv * _FoamTiling + flow * (_FlowSpeed * time);
                float  foamPattern = dot(
                    SAMPLE_TEXTURE2D(_FoamPattern, sampler_FoamPattern, foamUV).rgb,
                    float3(1.0, 1.0, 1.0)) / 3.0;
                foamPattern = smoothstep(_FoamThreshold,
                                         _FoamThreshold + _FoamSoftness,
                                         foamPattern);

                float foamAmount = 1.0 - saturate(thickness / max(_FoamDepth, 0.0001));
                foamAmount = pow(max(foamAmount, 0.0001), max(_FoamFalloff, 0.01));
                foamAmount = saturate(foamAmount * foamPattern * _FoamIntensity) * _FoamColor.a;
                color = lerp(color, _FoamColor.rgb, foamAmount);

                // ---------------------------------------------------------
                // 4. Specular highlight (base.frag), added last like
                //    base-combine.frag does. The fresnel factor tints the
                //    specular colour towards white at grazing angles.
                // ---------------------------------------------------------
                half4 specularMap = SAMPLE_TEXTURE2D(_SpecularMap, sampler_SpecularMap, input.uv);
                float3 halfwayDir = normalize(mainLight.direction + viewDirWS);
                float  ndh        = saturate(dot(normalWS, halfwayDir));
                float  shininess  = max(specularMap.g, 0.01) * 127.75;
                float  specular   = pow(ndh, shininess) * _SpecularIntensity;

                float fresnel = pow(1.0 - saturate(dot(halfwayDir, viewDirWS)),
                                    max(specularMap.b, 0.01) * 5.0);
                half3 specularColor = lerp(specularMap.rrr, half3(1, 1, 1), saturate(fresnel));
                color += mainLight.color * specular * specularColor * specularMap.r;

                if (_DebugView > 0.5)
                {
                    float view = floor(_DebugView + 0.5);
                    if (view < 1.5) { return half4(foamAmount, 0, 0, 1); }
                    if (view < 2.5) { return half4(depth01, 0, 0, 1); }
                    if (view < 3.5) { return half4(saturate(reflectionAlpha), 0, 0, 1); }
                    if (view < 4.5) { return half4(saturate(thickness * 0.25), 0, 0, 1); }
                    return half4(saturate(1.0 - _WaterBodyStrength * depth01), 0, 0, 1);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
