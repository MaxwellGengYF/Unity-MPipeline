Shader "Unlit/DrawLinearDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
		Cull off
		ZTest Less
        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            StructuredBuffer<float3x4> clusterBuffer;
            StructuredBuffer<uint> resultBuffer;
            StructuredBuffer<float3> verticesBuffer;
            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (uint vertexID : SV_VERTEXID, uint instanceID : SV_INSTANCEID)
            {
                v2f o;
                uint id = resultBuffer[instanceID];
                float3 worldPos = mul(clusterBuffer[id], float4(verticesBuffer[vertexID], 1));
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                return o;
            }

            float frag (v2f i) : SV_Target
            {
                return Linear01Depth(i.vertex.z);
            }
            ENDCG
        }
    }
}
