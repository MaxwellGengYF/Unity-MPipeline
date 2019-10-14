Shader "Unlit/VTDecal_Displacement"
{
    Properties
    {
       
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags {"LightMode" = "TerrainDisplacement" "Name" = "TerrainDisplacement"}
            BlendOp Max
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            Texture2DArray<float> _VirtualHeightmap;
            uint _OffsetIndex;
            float2 _HeightScaleOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                return o;
            }

            float WorldHeightToTexHeight(float worldHeight)
            {
                worldHeight -= _HeightScaleOffset.y;
                return worldHeight / _HeightScaleOffset.x;
            }
            
            float frag (v2f i) : SV_Target
            {
                return WorldHeightToTexHeight(i.worldPos.y);
            }
            ENDCG
        }
    }
}
