Shader "3DGameShaders/BaseLit"
{
    Properties
    {
        [NoScaleOffset] _DiffuseMap  ("Diffuse Map",  2D) = "white" {}
        [NoScaleOffset] _NormalMap   ("Normal Map",   2D) = "bump" {}
        [NoScaleOffset] _SpecularMap ("Specular Map", 2D) = "white" {}
        _EmissionColor   ("Emission Color", Color) = (0, 0, 0, 0)
        _Shininess       ("Shininess", Range(1, 128)) = 32
        _FresnelPower    ("Fresnel Power", Range(0, 5)) = 2
        _RimStrength     ("Rim Light Strength", Range(0, 3)) = 1.2
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.8
        _CelShading      ("Cel Shading", Range(0, 1)) = 0
        // Runtime toggles, matching the demo's keyboard switches.
        _NormalMapOn     ("Normal Mapping", Range(0, 1)) = 1
        _FresnelOn       ("Fresnel", Range(0, 1)) = 1
        _RimOn           ("Rim Lighting", Range(0, 1)) = 1
        _BlinnPhongOn    ("Blinn-Phong (0 = Phong)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float  _Shininess;
                float  _FresnelPower;
                float  _RimStrength;
                float  _AmbientStrength;
                float  _CelShading;
                float  _NormalMapOn;
                float  _FresnelOn;
                float  _RimOn;
                float  _BlinnPhongOn;
            CBUFFER_END

            TEXTURE2D(_DiffuseMap);  SAMPLER(sampler_DiffuseMap);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SpecularMap); SAMPLER(sampler_SpecularMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float3 viewDirWS  : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normalInputs   = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv         = input.uv;
                output.normalWS   = normalInputs.normalWS;
                output.tangentWS  = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.viewDirWS  = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 diffuseMap  = SAMPLE_TEXTURE2D(_DiffuseMap,  sampler_DiffuseMap,  input.uv);
                half4 specularMap = SAMPLE_TEXTURE2D(_SpecularMap, sampler_SpecularMap, input.uv);

                float3 normalWS = normalize(input.normalWS);
                if (_NormalMapOn > 0.5)
                {
                    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                    float sgn = input.tangentWS.w * unity_WorldTransformParams.w;
                    float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * sgn;
                    normalWS = normalize(
                        input.tangentWS.xyz * normalTS.x +
                        bitangentWS * normalTS.y +
                        input.normalWS * normalTS.z);
                }

                float3 viewDirWS = input.viewDirWS;

                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 lightCol = mainLight.color;

                float ndl = saturate(dot(normalWS, lightDir));
                if (_CelShading > 0.5)
                {
                    ndl = smoothstep(0.1, 0.2, ndl);
                }
                half3 diffuse = diffuseMap.rgb * lightCol * ndl;

                float3 halfway = normalize(lightDir + viewDirWS);
                float  ndh     = saturate(dot(normalWS, halfway));
                float  shininess = max(specularMap.g, 0.01) * 127.75;
                float  specIntensity;
                if (_BlinnPhongOn > 0.5)
                {
                    specIntensity = pow(ndh, shininess);
                }
                else
                {
                    // Phong: mirror the light about the normal and compare to the eye.
                    float3 reflectedDir = reflect(-lightDir, normalWS);
                    specIntensity = pow(saturate(dot(reflectedDir, viewDirWS)), shininess);
                }

                float fresnel = 1.0;
                if (_FresnelOn > 0.5)
                {
                    float3 fresnelBase = _BlinnPhongOn > 0.5 ? halfway : normalWS;
                    fresnel = pow(1.0 - saturate(dot(fresnelBase, viewDirWS)),
                                  max(specularMap.b, 0.01) * 5.0);
                }
                half3 specColor = lerp(specularMap.rrr, half3(1, 1, 1), clamp(fresnel, 0.0, 1.0));
                half3 specular = lightCol * specIntensity * specColor * specularMap.r;

                half rim = 0.0;
                if (_RimOn > 0.5)
                {
                    rim = 1.0 - saturate(dot(viewDirWS, normalWS));
                    if (_CelShading > 0.5)
                    {
                        rim = smoothstep(0.3, 0.4, rim);
                    }
                    else
                    {
                        rim = pow(rim, 2.0) * _RimStrength;
                    }
                }
                half3 rimLight = rim * diffuse;

                float up = normalWS.y * 0.5 + 0.5;
                half3 skyLight = lerp(half3(0.302, 0.451, 0.471), half3(0.765, 0.573, 0.400), up);
                half3 ambient  = diffuseMap.rgb * skyLight * _AmbientStrength;

                half3 emission = _EmissionColor.rgb;
                half3 color = ambient + diffuse + specular + rimLight + emission;

                return half4(color, diffuseMap.a);
            }
            ENDHLSL
        }
    }
}
