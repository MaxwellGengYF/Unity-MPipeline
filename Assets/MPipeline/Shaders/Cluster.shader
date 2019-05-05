Shader "Hidden/Cluster"
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
			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				Point v = getVertex(vertexID, instanceID); 
				float4 worldPos = float4(v.vertex, 1);
				v2f o;
				o.vertex = mul(UNITY_MATRIX_VP, worldPos);
				return o;
			}
			
			void frag (v2f i,
    out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3,
  out float depth : SV_TARGET4
) 
			{
				outGBuffer0 = float4(0.5,0.5,0.5,1);
				outGBuffer1 = float4(0.5,0.5,0.5,0.3);
				outGBuffer2 = float4(0, 0, 1, 1);
				outEmission = i.vertex.z;
				depth = i.vertex.z;
			}
			ENDCG
		}
	}
}
