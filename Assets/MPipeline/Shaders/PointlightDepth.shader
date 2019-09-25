Shader "Unlit/PointlightDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZTest less
        Cull back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "CGINC/Procedural.cginc"
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };
            float4x4 _ShadowMapVP;
            float4 _LightPos;
            v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) 
            {
                float3 v = getVertex(vertexID, instanceID);
                v2f o;
                o.worldPos = v;
                o.vertex = mul(_ShadowMapVP, float4(v, 1));
                return o;
            }

            half frag (v2f i) : SV_Target
            {
               return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
            } 
            ENDCG
        }

        Pass
        {
			ZTest less
            Cull back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "CGINC/Procedural.cginc"
			struct appdata_shadow
			{
				float4 vertex : POSITION;
			};
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };
            float4x4 _ShadowMapVP;
            float4 _LightPos;
            v2f vert (appdata_shadow v) 
            {
                v2f o;
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xyz;
                o.vertex = mul(_ShadowMapVP, worldPos);
                return o;
            }

            half frag (v2f i) : SV_Target
            {
               return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
            } 
            ENDCG
        }
    }
}
