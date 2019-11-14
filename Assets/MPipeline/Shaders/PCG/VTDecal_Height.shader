Shader "VTDecal/VTDecal_HeightBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [NoScaleOffset]_BumpMap("Bump", 2D) = "bump"{}
        [NoScaleOffset] _SMO("SMO", 2D) = "white"{}
        _HeightBlendScale("Height blend scale", float) = 0
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Occlusion("Occlusion Scale", Range(0,1)) = 1
        _NormalIntensity("Normal Intensity", Vector) = (1,1,0,0)
        _MetallicIntensity("Metallic Intensity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
CGINCLUDE
cbuffer UnityPerMaterial
{
	float _MetallicIntensity;
	float _Occlusion;
	float _Glossiness;
	float4 _Color;
	float2 _NormalIntensity;
    float _HeightBlendScale;
}
#include "../CGINC/VirtualTexture.cginc"
 sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            sampler2D _SMO;
            float4 _HeightScaleOffset;
            Texture2DArray<float> _VirtualHeightmap; SamplerState sampler_VirtualHeightmap; float4 _VirtualHeightmap_TexelSize;
            Texture2D<float4> _MaskIndexMap;
            float4 _MaskIndexMap_TexelSize;
            float2 _OffsetIndex;
            float4 _MaskScaleOffset;
inline float SampleHeight(float3 uvs[4], float2 weight)
{
  float4 result = float4(
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[0], 0),
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[1], 0),
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[2], 0),
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[3], 0)
  );
  result.xy = lerp(result.xy, result.zw, weight.y);
  return lerp(result.x, result.y, weight.x);
}

float GetVTHeight(float2 localUV)
{
    float3 uvs[4];
    float2 weight;
    GetBilinearVirtualTextureUV(_MaskIndexMap,_MaskIndexMap_TexelSize, _OffsetIndex, localUV, _VirtualHeightmap_TexelSize, uvs, weight);
    return SampleHeight(uvs, weight);
}
ENDCG
        Pass
        {
            Tags {"LightMode" = "TerrainDecal" "Name" = "TerrainDecal"}
            ZTest Less
            Cull back
            ZWrite on
            Blend srcAlpha oneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

           
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
                float3 tangent : TEXCOORD2;
                float4 binormal : TEXCOORD3;        //XYZ : binormal   W: height
                float3 screenUV : TEXCOORD4;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenUV = ComputeScreenPos(o.vertex).xyw;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
  	            o.tangent = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
		        o.normal =  mul((float3x3)unity_ObjectToWorld, v.normal);
  	            o.binormal.xyz = cross(v.normal, o.tangent.xyz) * v.tangent.w;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.binormal.w = worldPos.y;
                return o;
            }

            float GetBlendWeight(float worldHeight, float2 screenUV)
            {
                float vtHeight = GetVTHeight(screenUV * _MaskScaleOffset.x + _MaskScaleOffset.yz);
                float vtWorldHeight = vtHeight * _HeightScaleOffset.x + _HeightScaleOffset.y;
                float blendWeight = (worldHeight - vtWorldHeight) * _HeightBlendScale;
                return saturate(blendWeight);
            }

            void frag (v2f i, out float4 albedoOut : SV_TARGET0, out float4 normalOut : SV_TARGET1, out float4 smoOut : SV_TARGET2)
            {
                float2 screenUV = i.screenUV.xy / i.screenUV.z;
                float4 albedo = tex2D(_MainTex, i.uv) * _Color;
                albedo.w = GetBlendWeight(i.binormal.w, screenUV);
                float3 normal = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3x3 TBN = float3x3(normalize(i.tangent), normalize(i.binormal.xyz), normalize(i.normal));
                normal.xy *= _NormalIntensity.xy;
                normal = normalize(mul(normal, TBN));
                float3 smo = tex2D(_SMO, i.uv);
                smo.x *= _Glossiness;
                smo.y *= _MetallicIntensity;
                smo.z = lerp(1, smo.z, _Occlusion);
                albedoOut = albedo;
                normalOut = float4(normal.xz, 0, albedo.w);
                smoOut = float4(smo, albedo.w);
            }
            ENDCG
        }

        Pass
        {
            Tags {"LightMode" = "TerrainDisplacement" "Name" = "TerrainDisplacement"}
            Cull back
            ZWrite on
            ZTest Less
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 screenUV : TEXCOORD2;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                o.screenUV = ComputeScreenPos(o.vertex).xyw;
                return o;
            }
            
            float frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenUV.xy / i.screenUV.z;
                float vtHeight = GetVTHeight(screenUV * _MaskScaleOffset.x + _MaskScaleOffset.yz);
                 float vtWorldHeight = vtHeight * _HeightScaleOffset.x + _HeightScaleOffset.y;
                float heightDiff = i.worldPos.y - vtWorldHeight;
                float blendWeight = saturate(heightDiff * _HeightBlendScale);
                return saturate((lerp(vtWorldHeight, i.worldPos.y, blendWeight) - _HeightScaleOffset.y) / _HeightScaleOffset.x);
            }
            ENDCG
        }
    }
}
