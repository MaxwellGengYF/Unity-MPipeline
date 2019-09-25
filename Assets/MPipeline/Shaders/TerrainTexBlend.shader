Shader "Hidden/TerrainTexBlend"
{
    SubShader
    {
        Pass
        {
                    // No culling or depth
            Cull Off ZWrite Off ZTest Always
            Blend srcAlpha oneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "CGINC/TexBlendMask.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            void frag (v2f i, out float4 albedo : SV_TARGET0, out float4 normal : SV_TARGET1, out float4 smo : SV_TARGET2)
            {
                float3 outAlbedo, outSpec, outNormal;
                float outMask;
                GetBeforeBlendResult(i.uv, outAlbedo, outNormal, outSpec, outMask);
                outNormal.xy = outNormal.xy * 0.5 + 0.5;
                albedo = float4(outAlbedo, outMask);
                normal = float4(outNormal, outMask);
                smo = float4(outSpec, outMask);
            }
            ENDCG
        }

        pass
        {
            Cull Back
            ZWrite on
            ZTest LEqual
            Blend srcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 tangent : TANGENT;
	            float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldTangent : TEXCOORD1;
                float4 worldBinormal : TEXCOORD2;
                float4 worldNormal : TEXCOORD3;
            };
            float4x4 _VP;
            sampler2D _DecalAlbedo;
            sampler2D _DecalNormal;
            sampler2D _DecalSMO;
            sampler2D _DecalMask;
            float4 _DecalSMOWeight;
            float4 _DecalScaleOffset;

            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.vertex = mul(_VP, worldPos);
                o.uv = v.uv;
                v.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
  	            o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	            v.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
		        o.worldNormal =float4(v.normal, worldPos.z);
  	            o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
                return o;
            }

            void frag(v2f i, out float4 albedo : SV_TARGET0, out float4 normal : SV_TARGET1, out float4 smo : SV_TARGET2)
            {
                float3x3 wdMatrix= float3x3(normalize(i.worldTangent.xyz), normalize(i.worldBinormal.xyz), normalize(i.worldNormal.xyz));
                float mask = tex2D(_DecalMask, i.uv) * _DecalSMOWeight.w;
                i.uv = i.uv * _DecalScaleOffset.xy + _DecalScaleOffset.zw;
                albedo = float4(tex2D(_DecalAlbedo, i.uv).xyz, mask);
                normal = float4(mul(UnpackNormal(tex2D(_DecalNormal, i.uv)), wdMatrix), mask);
                smo = float4(tex2D(_DecalSMO, i.uv).xyz, mask);
                smo.b = lerp(1, smo.b, _DecalSMOWeight.b);
                smo.xy *= _DecalSMOWeight.xy;
            }
            ENDCG
        }
    }
}
