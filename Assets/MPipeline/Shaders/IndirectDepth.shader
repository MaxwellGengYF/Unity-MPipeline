Shader "Unlit/IndirectDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            
            struct v2f
            {
                float3 worldPos : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            StructuredBuffer<float3x4> resultBuffer;
            StructuredBuffer<float4> verticesBuffer;
            v2f vert (uint vertexID : SV_VERTEXID, uint instanceID : SV_INSTANCEID)
            {
                v2f o;
                float3 vertex = verticesBuffer[vertexID];
                float3x4 info = resultBuffer[instanceID];
                o.worldPos =  mul(info, float4(vertex, 1));
                o.vertex = mul(UNITY_MATRIX_VP, float4(o.worldPos, 1));
                return o;
            }

            half frag (v2f i) : SV_Target
            {
                return Linear01Depth(i.vertex.z);
            }
            ENDCG
        }
    }
}
