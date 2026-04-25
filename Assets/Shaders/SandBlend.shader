Shader "Custom/SandBlend"
{
    Properties
    {
        _MainTex ("Level Sand (Color)", 2D) = "white" {}
        _MainNormal ("Level Sand (Normal)", 2D) = "bump" {}
        _MainAO ("Level Sand (AO)", 2D) = "white" {}
        _MainSmoothness ("Level Smoothness", Range(0,1)) = 0.2

        _OutsideTex ("Outside Sand (Color)", 2D) = "white" {}
        _OutsideNormal ("Outside Sand (Normal)", 2D) = "bump" {}
        _OutsideAO ("Outside Sand (AO)", 2D) = "white" {}
        _OutsideSmoothness ("Outside Smoothness", Range(0,1)) = 0.2

        _Tiling ("Texture Tiling", Float) = 1

        _GridMinX ("Grid Min X", Float) = 0
        _GridMinZ ("Grid Min Z", Float) = 0
        _GridMaxX ("Grid Max X", Float) = 50
        _GridMaxZ ("Grid Max Z", Float) = 50
        _BlendWidth ("Blend Width (world units)", Float) = 5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_MainNormal);     SAMPLER(sampler_MainNormal);
            TEXTURE2D(_MainAO);         SAMPLER(sampler_MainAO);
            TEXTURE2D(_OutsideTex);     SAMPLER(sampler_OutsideTex);
            TEXTURE2D(_OutsideNormal);  SAMPLER(sampler_OutsideNormal);
            TEXTURE2D(_OutsideAO);      SAMPLER(sampler_OutsideAO);

            CBUFFER_START(UnityPerMaterial)
                float _MainSmoothness;
                float _OutsideSmoothness;
                float _Tiling;
                float _GridMinX;
                float _GridMinZ;
                float _GridMaxX;
                float _GridMaxZ;
                float _BlendWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // World-space tiled UVs for seamless tiling
                float2 tiledUV = IN.positionWS.xz * _Tiling;

                // Sample both texture sets
                half4 levelColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, tiledUV);
                half3 levelNorm = UnpackNormal(SAMPLE_TEXTURE2D(_MainNormal, sampler_MainNormal, tiledUV));
                half levelAO = SAMPLE_TEXTURE2D(_MainAO, sampler_MainAO, tiledUV).r;

                half4 outsideColor = SAMPLE_TEXTURE2D(_OutsideTex, sampler_OutsideTex, tiledUV);
                half3 outsideNorm = UnpackNormal(SAMPLE_TEXTURE2D(_OutsideNormal, sampler_OutsideNormal, tiledUV));
                half outsideAO = SAMPLE_TEXTURE2D(_OutsideAO, sampler_OutsideAO, tiledUV).r;

                // Compute blend factor based on distance from grid bounds
                float dLeft  = IN.positionWS.x - _GridMinX;
                float dRight = _GridMaxX - IN.positionWS.x;
                float dBottom = IN.positionWS.z - _GridMinZ;
                float dTop   = _GridMaxZ - IN.positionWS.z;
                float distInside = min(min(dLeft, dRight), min(dBottom, dTop));

                // 0 = outside/edge (outside tex), 1 = inside (level tex)
                float blend = saturate(distInside / _BlendWidth);

                // Lerp everything
                half3 albedo = lerp(outsideColor.rgb, levelColor.rgb, blend);
                half3 normal = normalize(lerp(outsideNorm, levelNorm, blend));
                half ao = lerp(outsideAO, levelAO, blend);
                half smoothness = lerp(_OutsideSmoothness, _MainSmoothness, blend);

                // Build TBN and transform normal to world space
                float3x3 TBN = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3 normalWS = normalize(mul(normal, TBN));

                // URP lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = 0;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = normal;
                surfaceData.occlusion = ao;
                surfaceData.alpha = 1;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth prepass — required so the floor participates in the depth texture
        // that Screen Space Shadows / SSAO sample.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthOnlyVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthOnlyFrag(DepthVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth+normals prepass — used by SSAO with normal samples.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            DNVaryings DepthNormalsVert(DNAttributes IN)
            {
                DNVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DepthNormalsFrag(DNVaryings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
