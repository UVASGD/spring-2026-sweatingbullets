Shader "Custom/SandBlend"
{
    Properties
    {
        _MainTex ("Level Sand (Color)", 2D) = "white" {}
        _MainNormal ("Level Sand (Normal)", 2D) = "bump" {}
        _MainAO ("Level Sand (AO)", 2D) = "white" {}
        _MainSmoothness ("Level Smoothness", Range(0,1)) = 0.2

        _CobbleTex ("Level Cobble (Color)", 2D) = "white" {}
        _CobbleNormal ("Level Cobble (Normal)", 2D) = "bump" {}
        _CobbleAO ("Level Cobble (AO)", 2D) = "white" {}
        _CobbleSmoothness ("Level Cobble Smoothness", Range(0,1)) = 0.2

        _RoadMaskTex("Level Mask", 2D) = "white" {}

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

        _ShadowStrength ("Shadow Strength", Range(0,1)) = 1.0
        _ShadowTint ("Shadow Tint", Color) = (0,0,0,1)
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
            TEXTURE2D(_CobbleTex);      SAMPLER(sampler_CobbleTex);
            TEXTURE2D(_CobbleNormal);   SAMPLER(sampler_CobbleNormal);
            TEXTURE2D(_RoadMaskTex);    SAMPLER(sampler_RoadMaskTex);
            TEXTURE2D(_CobbleAO);       SAMPLER(sampler_CobbleAO);
            TEXTURE2D(_OutsideTex);     SAMPLER(sampler_OutsideTex);
            TEXTURE2D(_OutsideNormal);  SAMPLER(sampler_OutsideNormal);
            TEXTURE2D(_OutsideAO);      SAMPLER(sampler_OutsideAO);

            CBUFFER_START(UnityPerMaterial)
                float _MainSmoothness;
                float _CobbleSmoothness;
                float _OutsideSmoothness;
                float4 _PoissonOffsets[16];
                float _Tiling;
                float _GridMinX;
                float _GridMinZ;
                float _GridMaxX;
                float _GridMaxZ;
                float _BlendWidth;
                float _ShadowStrength;
                float4 _ShadowTint;
            CBUFFER_END
            
            int _PoissonCount;

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

                // Sample Cobble set
                half4 cobbleColor = SAMPLE_TEXTURE2D(_CobbleTex, sampler_CobbleTex, tiledUV);
                half3 cobbleNorm = UnpackNormal(SAMPLE_TEXTURE2D(_CobbleNormal, sampler_CobbleNormal, tiledUV));
                half cobbleAO = SAMPLE_TEXTURE2D(_CobbleAO, sampler_CobbleAO, tiledUV).r;

                // Sample both texture sets
                half4 sandColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, tiledUV);
                half3 sandNorm = UnpackNormal(SAMPLE_TEXTURE2D(_MainNormal, sampler_MainNormal, tiledUV));
                half sandAO = SAMPLE_TEXTURE2D(_MainAO, sampler_MainAO, tiledUV).r;

                half4 outsideColor = SAMPLE_TEXTURE2D(_OutsideTex, sampler_OutsideTex, tiledUV);
                half3 outsideNorm = UnpackNormal(SAMPLE_TEXTURE2D(_OutsideNormal, sampler_OutsideNormal, tiledUV));
                half outsideAO = SAMPLE_TEXTURE2D(_OutsideAO, sampler_OutsideAO, tiledUV).r;

                // Compute blend factor based on distance from grid bounds
                float dLeft  = IN.positionWS.x - _GridMinX;
                float dRight = _GridMaxX - IN.positionWS.x;
                float dBottom = IN.positionWS.z - _GridMinZ;
                float dTop   = _GridMaxZ - IN.positionWS.z;
                float distInside = min(min(dLeft, dRight), min(dBottom, dTop));

                float2 dUV = float2(dLeft / (_GridMaxX - _GridMinX), dBottom / (_GridMaxZ - _GridMinZ));

                float2 texelID = floor(dUV * float2(_GridMaxX - _GridMinX, _GridMaxZ - _GridMinZ));
                float randomAngle = frac(sin(dot(texelID, float2(127.1, 311.7))) * 43758.5453) * 6.2832;

                float cosA = cos(randomAngle);
                float sinA = sin(randomAngle);

                half maskResult = 0;
                for (int i = 0; i < _PoissonCount; i++)
                {
                    float2 o = _PoissonOffsets[i].xy;
                    float2 offset = float2(cosA * o.x - sinA * o.y, sinA * o.x + cosA * o.y);
                    maskResult += SAMPLE_TEXTURE2D_LOD(_RoadMaskTex, sampler_RoadMaskTex, dUV + offset, 0).r;
                }
                maskResult = _PoissonCount > 0 ? maskResult / (half)_PoissonCount : 1.0;
                maskResult = smoothstep(0.2, 0.8, maskResult);

                half4 levelColor = lerp(cobbleColor, sandColor, maskResult);
                half3 levelNorm = lerp(cobbleNorm, sandNorm, maskResult);
                half levelAO = lerp(cobbleAO, sandAO, maskResult);
                half levelSmoothness = lerp(_CobbleSmoothness, _MainSmoothness, maskResult);

                // 0 = outside/edge (outside tex), 1 = inside (level tex)
                float blend = saturate(distInside / _BlendWidth);

                // Lerp everything
                half3 albedo = lerp(outsideColor.rgb, levelColor.rgb, blend);
                half3 normal = normalize(lerp(outsideNorm, levelNorm, blend));
                half ao = lerp(outsideAO, levelAO, blend);
                half smoothness = lerp(_OutsideSmoothness, levelSmoothness, blend);

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

                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);

                // Per-material shadow strength override.
                // Compute the raw realtime shadow (1 = lit, 0 = fully shadowed) and
                // mix back the missing main-light diffuse contribution proportional
                // to (1 - _ShadowStrength). _ShadowTint lets you push the in-shadow
                // color (e.g. cool/blue) without changing the lit areas.
                Light mainLight = GetMainLight();
                half rawShadow = MainLightRealtimeShadow(inputData.shadowCoord);
                half shadowMask = 1.0 - rawShadow; // 0 outside shadow, 1 inside
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 unshadowedDiffuse = albedo * mainLight.color * NdotL * ao;

                finalColor.rgb += unshadowedDiffuse * shadowMask * (1.0 - _ShadowStrength);
                finalColor.rgb = lerp(finalColor.rgb, finalColor.rgb * _ShadowTint.rgb, shadowMask * _ShadowTint.a * _ShadowStrength);

                return finalColor;
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
