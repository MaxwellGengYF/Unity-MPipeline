Shader "Unlit/MPipelineOpaqueOverride"
{
    SubShader
    {
       	Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "Shadow"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#pragma multi_compile __ POINT_LIGHT_SHADOW
			#pragma multi_compile __ USE_DECAL
			
			float4x4 _ShadowMapVP;
			float4 _LightPos;
			struct appdata_shadow
			{
				float4 vertex : POSITION;
			};
			struct v2f
			{
				float4 vertex : SV_POSITION;
				#if POINT_LIGHT_SHADOW
				float3 worldPos : TEXCOORD1;
				#endif
			};

			v2f vert (appdata_shadow v)
			{
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				v2f o;
				#if POINT_LIGHT_SHADOW
				o.worldPos = worldPos.xyz;
				#endif
				o.vertex = mul(_ShadowMapVP, worldPos);
				return o;
			}

			
			float frag (v2f i)  : SV_TARGET
			{
				#if POINT_LIGHT_SHADOW
				return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
				#else
				return i.vertex.z;
				#endif
			}

			ENDCG
		}
		Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "Depth"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			
			struct appdata_depthPrePass
			{
				float4 vertex : POSITION;
			};
			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata_depthPrePass v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			void frag ()
			{
			}

			ENDCG
		}
	
    }
}