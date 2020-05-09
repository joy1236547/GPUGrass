﻿      Shader "Instanced/renderGrass" {
    Properties {
        _Color("Grass color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _AlphaTex("Alpha (A)", 2D) = "white" {}
        _Height("Grass Height", float) = 3
        _Width("Grass Width", range(0, 0.1)) = 0.05
        _SectionCount("section count", int) = 5
        _Shadow("shadow density", range(0, 1)) = 0.5
        _num("number", range(50,1000)) = 500
    }

    SubShader {

        Pass {

            Tags {"LightMode"="ForwardBase" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
            AlphaToMask On
            Cull Off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma target 4.5

            #include "UnityCG.cginc"
            #include "Data.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #if SHADER_TARGET >= 45
                StructuredBuffer<float3> renderPosAppend;
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 bladeInfo: TEXCOORD2;
                //float4 shadowPos:TEXCOORD3;
                //float2 test: TEXCOORD4;
                //SHADOW_COORDS(4)
            };


            struct GrassData {
                float height, density;
                float4 rootDir;
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            sampler2D _ShadowTex;
            float4 _MainTex_ST;
            float _num;

            float _Height;//草的高度
            float _Width;//草的宽度
            float _Shadow;//阴影强度
            int _minSection,_maxSection, _curSection;//草叶的分段数
            float4 _Color;

            static const float oscillateDelta = 0.05;
            static const float PI = 3.14159;

            int maxGrassCount, minGrassCount;
            float zFar;
            float4 camPos;

            //sampler2D _ShadowMapTexture;

            Texture3D<float2> mathData;

            StructuredBuffer<GrassData> _patchData;
            int pregenerateGrassAmount;
            int grassAmountPerTile;


            float3 TerrainBilinear(float3 hd, float3 input) {
                float t = _TileSize;
                float yR1 = input.x / t * hd.x;
                float yR2 = (t - input.x) / t * hd.z + input.x / t * hd.y;
                float y = (t - input.z) / t * yR1 + input.z / t * yR2;
                return float3(input.x, y, input.z);
            }

            fixed rand(float3 value) {
                return frac(sin(dot(value, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
            }

            int setRandomPatchIndex(float3 index) {
                float random = rand(index);
                return (int)(random * (pregenerateGrassAmount - grassAmountPerTile));
            }

            //@UnityCG.cginc 859
            float4 ClipSpaceShadowCasterPos(float3 wPos, float3 wNormal)
            {

                if (unity_LightShadowBias.z != 0.0)
                {
                    float3 wLight = normalize(UnityWorldSpaceLightDir(wPos.xyz));

                    // apply normal offset bias (inset position along the normal)
                    // bias needs to be scaled by sine between normal and light direction
                    // (http://the-witness.net/news/2013/09/shadow-mapping-summary-part-1/)
                    //
                    // unity_LightShadowBias.z contains user-specified normal offset amount
                    // scaled by world space texel size.

                    float shadowCos = dot(wNormal, wLight);
                    float shadowSine = sqrt(1 - shadowCos * shadowCos);
                    float normalBias = unity_LightShadowBias.z * shadowSine;

                    wPos.xyz -= wNormal * normalBias;
                }

                return mul(UNITY_MATRIX_VP, wPos);
            }

            //形成草叶形状
            float3 getBladeOffset(float3 index, float3 vertex,
                float uvv, GrassData patchInfo, int patchIndex, out float3 normal, out float2 basicShape) {
                //变量准备
                uint vertIndex = vertex.y;//0~11
                float density = patchInfo.density;
                float4 rootLPos = patchInfo.rootDir.xyzz;
                int secIndex = vertIndex / 2;
                float3 ee, ew, en;
                float sin, cos;

                float dir = patchInfo.rootDir.w * 2 * PI,
                    height = patchInfo.height * _Height;
                normal = float3(0, 0, 1);
                //基础形态：叶片在xy平面
                float3 bladeOffset = float3(
                    (fmod(vertIndex, 2) * 2 - 1) * _Width, uvv *height, 0);
                basicShape = bladeOffset.xy;
                ee = float3(0, 1, 0); ew = float3(1, 0, 0), en = cross(ee, ew);

                //风
                //float3 windVec = float3(1, 0, 0);

                //blade bending
                float c = 1.2;//(0,0), (bending, c*height)
                float bending = fmod(patchIndex, 3)*0.5 + 0.2;
                float a = -c * height / (bending * bending), b = 2 * c * height / bending;
                float deltaZ = (-b + sqrt(abs(b*b + 4 * a*(uvv * height)))) / (2 * a);
                float yo = 2 * a*deltaZ + b+0.001;//>0
                float k2 = -1 / yo;
                normal = float3(0, -k2, -1);
                bladeOffset.z += deltaZ;
                /*sincos()
                ee=*/
                //new bending
                float bendTheta = 60.5;
                float2 cossinSum = mathData[float3(floor(bendTheta), _curSection - _minSection, secIndex)];
                
                //blade swinging
                sincos(dir, sin, cos);
                bladeOffset = float3(bladeOffset.x*cos + bladeOffset.z*sin,
                    bladeOffset.y,
                    -bladeOffset.x*sin + bladeOffset.z*cos);
                normal = float3(normal.x*cos + normal.z*sin,
                    normal.y,
                    -normal.x*sin + normal.z*cos);
                //blade twisting
                float tangle = PI / 3.0;//0~2pi
                //bladeOffset= RotateArbitraryLine()

                normal = normalize(normal);
                return bladeOffset;
            }

            float3 getLocalRootPos(float3 index, float3 vertex, GrassData patchInfo) {
                float3 heightDelta;
                float height = getTerrainPos(index.xz).y;
                heightDelta.x = getTerrainPos(float2(index.x + 1, index.z)).y - height;
                heightDelta.y = getTerrainPos(float2(index.x + 1, index.z + 1)).y - height;
                heightDelta.z = getTerrainPos(float2(index.x, index.z + 1)).y - height;
                float3 rootLPos = patchInfo.rootDir.xyz;
                return TerrainBilinear(heightDelta, rootLPos.xyz);
            }

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);

            #if SHADER_TARGET >= 45
                float3 index = renderPosAppend[instanceID];
            #else
                float3 index = 0;
            #endif

                int localBladeIndex = v.vertex.x;//0~63
                int vertIndex = v.vertex.y;//0~11
                int rand = setRandomPatchIndex(index);
                int patchIndex = localBladeIndex + rand;//0~63+0~1023-64
                GrassData patchInfo = _patchData[patchIndex];
                float4 worldStartPos = getTerrainPos(index.xz);
                float3 wNormal = 0;
                float2 basicShape;
                //grass density
                //返回的话就不显示了
                fixed density = patchInfo.density;
                if (patchInfo.density > getTerrainDensity(index.xz)) { return o; }
                
                //lod
                float dist = abs(distance(camPos, worldStartPos.xyz));
                int lodCount = (minGrassCount - maxGrassCount) / zFar * dist + maxGrassCount - 1;
                if (localBladeIndex > lodCount) {
                    return o;
                }

                float3 localPosition = 0;
                /*localPosition += float3(bladeIndex, 0, bladeIndex / 63);//local root pos
                localPosition += float3(vertIndex % 2/10.0, vertIndex / 2/5.0, 0);*/
                localPosition += getLocalRootPos(index, v.vertex.xyz, patchInfo);
                float3 bladeOffset = getBladeOffset(index, v.vertex.xyz, v.uv.y, patchInfo, patchIndex, wNormal, basicShape);
                localPosition += bladeOffset;
                float3 worldPosition = worldStartPos + localPosition;

                /*o.shadowPos = ClipSpaceShadowCasterPos(worldPosition, wNormal);
                o.shadowPos = UnityApplyLinearShadowBias(o.shadowPos);*/

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPosition, 1.0f));
                o.normal = wNormal;
                o.bladeInfo = float3(basicShape, patchIndex);
                //TRANSFER_SHADOW(o)
                return o;
            }



            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv)*_Color;
                //color.r += _yellow * i.bladeInfo.y;
                fixed4 alpha = tex2D(_AlphaTex, i.uv);

                clip(alpha - 0.5);
                //clip(-1);

                //fixed shadow = SHADOW_ATTENUATION(i);
                float3 lightDir = UnityWorldSpaceLightDir(i.pos);
                //float x = abs(i.bladeInfo.x);
                //return float4(x.xxx, 1);
                //return float4(abs(i.normal), alpha.r);
                half3 worldNormal = i.normal;// UnityObjectToWorldNormal(i.normal);

                //return float4(color.rgb, alpha.r);
                //shadow
                float2 tex;
                fixed f = frac(abs(lightDir.x) + abs(lightDir.y) + abs(lightDir.z));
                tex.x= frac(i.bladeInfo.z * f / _num) +i.bladeInfo.x;
                tex.y = i.bladeInfo.y / _Height;
                float bladeShadow = tex2D(_ShadowTex, tex).x;
                bladeShadow = bladeShadow * _Shadow + 1.0 - _Shadow;

                //return float4(tex,0, 1);

                //ads
                fixed3 light;

                //ambient
                fixed3 ambient = ShadeSH9(half4(worldNormal, 1));
                //ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;

                //diffuse
                fixed lambert = saturate(abs(dot(worldNormal, lightDir)));
                lambert = lambert * 0.5 + 0.5;//half lambert
                fixed3 diffuseLight = lambert * _LightColor0/* * shadow*/ * bladeShadow;
                //fixed3 worldLightDir = UnityWorldSpaceLightDir(i.pos);
                //diffuseLight= _LightColor0.rgb*color.rgb*saturate(dot(worldNormal,worldLightDir))

                //specular Blinn-Phong 
                fixed3 halfVector = normalize(UnityWorldSpaceLightDir(i.pos) + WorldSpaceViewDir(i.pos));
                fixed3 specularLight = pow(saturate(dot(worldNormal, halfVector)), 15) * _LightColor0;

                //light = ambient + diffuseLight / 2.0 + 0.5 + specularLight;
                light = ambient + diffuseLight  + specularLight;
                return float4(color.rgb * light, alpha.r);
            }

            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            { 
                "LightMode" = "ShadowCaster" 
                "IgnoreProjector" = "True"
            }

            ZWrite On

            CGPROGRAM
                #pragma target 3.0

                #pragma shader_feature _ALPHAPREMULTIPLY_ON
                #pragma multi_compile_shadowcaster

                #pragma vertex vertShadowCaster
                #pragma fragment fragShadowCaster

                #include "UnityStandardShadow.cginc"

            ENDCG
        }
    }
}