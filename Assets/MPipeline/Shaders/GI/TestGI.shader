Shader "Unlit/TestGI"
{
    	Properties
	{
		_Count("Level", Range(0, 6)) = 0
        _Mip("Mip", Range(0, 10)) = 0
	}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Transparent" "LightMode" = "Transparent"}
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "UnityCG.cginc"
            float _Count;
            float _Mip;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 objVert : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.objVert = v.uv;
                return o;
            }
            Texture2DArray<float3> _Cubemap; SamplerState sampler_Cubemap;
            float3 frag (v2f i) : SV_Target
            {
                return _Cubemap.SampleLevel(sampler_Cubemap, float3(i.objVert, _Count), _Mip);
            }
            ENDCG
        }
    }
}
