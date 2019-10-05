Shader "Unlit/SRPShadow"
{
  
    SubShader
    {
        CGINCLUDE
        float4 _LightPos;
        #pragma target 5.0
        ENDCG
       	Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "DirectionalLight"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#include "CGINC/Procedural.cginc"
			float4x4 _ShadowMapVP;
			float4 _NormalBiases;
			struct appdata_shadow
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};
			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata_shadow v)
			{
				float4 worldPos = float4(mul(unity_ObjectToWorld, v.vertex) - _NormalBiases.x * mul((float3x3)unity_ObjectToWorld, v.normal), 1);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
				return o;
			}

			
			float frag (v2f i)  : SV_TARGET
			{
				return i.vertex.z;
			}

			ENDCG
		}

		Pass
        {
			Tags {"LightMode"="PointLightPass"}
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
            float4x4 _VP;
            v2f vert (appdata_shadow v) 
            {
                v2f o;
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xyz;
                o.vertex = mul(_VP, worldPos);
				
                return o;
            }

            float frag (v2f i) : SV_Target
            {
               return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
            } 
            ENDCG
        }

		Pass
		{
			Tags {"LightMode"="SpotLightPass"}
			ZTest less
			Cull back
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#include "CGINC/Procedural.cginc"
			float4x4 _ShadowMapVP;
			float _LightRadius;
			struct v2f
			{
				float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
			};
			struct appdata_shadow
			{
				float4 vertex : POSITION;
			};

			v2f vert (appdata_shadow v)
			{
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
				o.worldPos = worldPos.xyz;
				return o;
			}
			float frag (v2f i) : SV_TARGET
			{
				return 0;
			}

			ENDCG
		}
    }
}
