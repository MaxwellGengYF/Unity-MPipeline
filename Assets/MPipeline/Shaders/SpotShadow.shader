Shader "Hidden/SpotShadow"
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
            #pragma target 5.0
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#include "CGINC/Procedural.cginc"
			float4x4 _ShadowMapVP;
            float3 _LightPos;
            float _LightRadius;
			struct v2f
			{
				float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
			};

			v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				float3 v = getVertex(vertexID, instanceID); 
				float4 worldPos = float4(v, 1);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
                o.worldPos = worldPos.xyz;
				return o;
			}
			float frag (v2f i) : SV_TARGET
			{
				return i.vertex.z;
			}

			ENDCG
		}

	}
}
