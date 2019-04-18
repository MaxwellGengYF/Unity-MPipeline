Shader "Hidden/ShadowDepth"
{
	SubShader
	{
		ZTest less
		Cull back
		Tags {"RenderType" = "Opaque"}
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#include "CGINC/Procedural.cginc"
			float4x4 _ShadowMapVP;
			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				Point v = getVertex(vertexID, instanceID); 
				float4 worldPos = float4(v.vertex, 1);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
				return o;
			}
			
			float frag (v2f i) : SV_Target
			{
				return i.vertex.z;
			}
			ENDCG
		}
		Pass
		{
			ZTest less
			Cull off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#include "CGINC/Procedural.cginc"
			float4x4 _ShadowMapVP;
			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				Point v = getVertex(vertexID, instanceID); 
				float4 worldPos = float4(v.vertex, 1);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
				
				return o;
			}
			
			float frag (v2f i) : SV_Target
			{
				return i.vertex.z;
			}
			ENDCG
		}
	}
}
