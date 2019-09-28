Shader "Unlit/VTTest"
{
    Properties
    {
        _TileOffset("Tile Offset", vector) = (1,1,0,0)
        _MainTex ("maintex", 2D) = "white"{}
        _Level("level", float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
    Stencil
	{
		Ref 0
		WriteMask 15
		Pass replace
		comp always
	}
Name "GBuffer"
Tags {"LightMode" = "GBuffer" "Name" = "GBuffer"}
ZTest LEqual
ZWrite on
Cull back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #include "CGINC/VirtualTexture.cginc"
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };
float4 _TileOffset;
            Texture2D<float4> _MainTex; SamplerState sampler_MainTex;
float _Level;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * _TileOffset.xy + _TileOffset.zw;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            Texture2DArray<float4> _ColorVT; SamplerState sampler_ColorVT;
            float4 frag (v2f i) : SV_Target3
            {
                return SampleVirtualTextureLevel(_ColorVT, sampler_ColorVT, floor(i.uv), frac(i.uv), 0);
            }
            ENDCG
        }
    }
}
