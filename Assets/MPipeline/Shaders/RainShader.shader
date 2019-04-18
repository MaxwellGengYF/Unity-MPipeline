Shader "RainShader"
{
	Properties
	{
		_Frequency("Frequency", Range(1, 200)) = 10
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always
		Blend one one
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			StructuredBuffer<float3> allPoints;

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float time : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};
			#define SIZE 0.02
			#define PI4 12.566370614359172
			static const float2 projectionPoints[4] = 
			{
				float2(SIZE, SIZE),
				float2(SIZE, -SIZE),
				float2(-SIZE, -SIZE),
				float2(-SIZE, SIZE)
			};

			static const float2 uvPoints[4] = 
			{
				float2(1, 1),
				float2(1, 0),
				float2(0, 0),
				float2(0, 1)
			};
			float _Frequency;
			v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				v2f o;
				float3 pt = allPoints[instanceID];
				o.vertex = float4(pt.xy + projectionPoints[vertexID], 0.5, 1);
				o.time = pt.z * 0.5;
				o.uv = uvPoints[vertexID];
				return o;
			}
			
			float2 frag (v2f i) : SV_Target
			{
				float2 uvDifference = i.uv - 0.5;
				float dist = length(uvDifference);
				clip(0.5 - dist);
				dist -= i.time;
				dist *= 15;
				dist = clamp(dist, 0, PI4);
				uvDifference *= sin(dist);
				return uvDifference;
			}
			ENDCG
		}
	}
}
