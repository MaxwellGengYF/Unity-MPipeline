Shader "VTDecal/VTDecal_HeightBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [NoScaleOffset]_BumpMap("Bump", 2D) = "bump"{}
        [NoScaleOffset] _SMO("SMO", 2D) = "white"{}
        _HeightBlendScale("Height blend scale", Range(0, 1)) = 0
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

        Pass
        {
            Tags {"LightMode" = "TerrainDecal" "Name" = "TerrainDecal"}
            ZTest Always
            Cull back
            ZWrite off
            Blend srcAlpha oneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

cbuffer UnityPerMaterial
{
	float _MetallicIntensity;
	float _Occlusion;
	float _Glossiness;
	float4 _Color;
	float2 _NormalIntensity;
    float _HeightBlendScale;
}

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            sampler2D _SMO;
            float2 _HeightScaleOffset;
            Texture2DArray<float> _VirtualHeightmap; SamplerState sampler_VirtualHeightmap;
            float _Count;
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
                float height = (worldPos.y - _HeightScaleOffset.y) / _HeightScaleOffset.x;
                o.binormal.w = height;
                return o;
            }

            void frag (v2f i, out float4 albedoOut : SV_TARGET0, out float4 normalOut : SV_TARGET1, out float4 smoOut : SV_TARGET2)
            {
                float2 screenUV = i.screenUV.xy / i.screenUV.z;
                float screenHeight = _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, float3(screenUV, _Count), 0);
                float heightDifference = i.binormal.w - screenHeight;
                float alphaBlend = heightDifference / max(1e-4, _HeightBlendScale);
                float4 albedo = tex2D(_MainTex, i.uv) * _Color;
                albedo.w *= alphaBlend;
                float3 normal = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3x3 TBN = float3x3(normalize(i.tangent) * _NormalIntensity.x, normalize(i.binormal.xyz) * _NormalIntensity.y, normalize(i.normal));
                normal = normalize(mul(normal, TBN));
                float3 smo = tex2D(_SMO, i.uv);
                smo.x *= _Glossiness;
                smo.y *= _MetallicIntensity;
                smo.z = lerp(1, smo.z, _Occlusion);
                albedoOut = albedo;
                normalOut = float4(normal.xzy, albedo.w);
                smoOut = float4(smo, albedo.w);
            }
            ENDCG
        }
    }
}
