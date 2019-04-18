Shader "Unlit/Terrain"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CGINC/Terrain.cginc"
            StructuredBuffer<uint> triangleBuffer;
            StructuredBuffer<float2> verticesBuffer;
            StructuredBuffer<TerrainPanel> clusterBuffer;
            StructuredBuffer<uint> resultBuffer;
            StructuredBuffer<float> heightMapBuffer;
            uint _MeshSize;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
                nointerpolation int4 texID : TEXCOORD1;
            };

            v2f vert (uint vertexID : SV_VERTEXID, uint instanceID : SV_INSTANCEID)
            {
                //Prepare appdata
                TerrainPanel panel = clusterBuffer[resultBuffer[instanceID]];
                uint triID = triangleBuffer[vertexID];
                const int vertSize = _MeshSize + 1;
                const int heightMapSize = vertSize * vertSize;
                //Edge vertex
                uint2 vertCoord = GetIndex(triID, vertSize);
                bool2 vertCoordEven = (vertCoord % 2 == 1);
                bool4 vertCoordEdge =  (vertCoord.xyxy == uint4(0, 0, _MeshSize, _MeshSize));
                bool4 panelEnabled = panel.edgeFlag & uint4(1,2,4,8);
                vertCoord += vertCoordEven.xy && ((vertCoordEdge.yx && panelEnabled.xz) || (vertCoordEdge.wz && panelEnabled.yw));
                triID = GetIndex(vertCoord, vertSize);
                //Prepare v2f data
                v2f o;
                float2 pos = verticesBuffer[triID] * panel.extent.xz * 2 + panel.position.xz;
                o.worldPos = float3(pos.x, heightMapBuffer[panel.heightMapIndex * heightMapSize + triID], pos.y);
                o.vertex = mul(UNITY_MATRIX_VP, float4(o.worldPos, 1));
                o.texID = panel.textureIndex;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return 1;
            }
            ENDCG
        }
    }
}
