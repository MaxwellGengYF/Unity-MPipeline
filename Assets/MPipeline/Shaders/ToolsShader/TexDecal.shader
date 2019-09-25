Shader "Hidden/TexDecal"
{
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
            #pragma multi_compile __ USE_VORONOI_SAMPLE
            #include "UnityCG.cginc"
            sampler2D _MaskTex;
            float _BlendVar;
            float4x4 _WorldToLocalMatrix;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv2 : TEXCOORD1;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float3 objectPos : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = float4(v.uv2 * 2 - 1, 1, 1);
                #if UNITY_UV_STARTS_AT_TOP
                o.vertex.y = -o.vertex.y;
                #endif 
                o.objectPos =mul(_WorldToLocalMatrix, v.vertex).xyz;
                o.normal = mul((float3x3)_WorldToLocalMatrix, v.normal);
                return o;
            }

            float4 hash4( float2 p ) { return frac(sin(float4( 1.0+dot(p,float2(37.0,17.0)), 
                                              2.0+dot(p,float2(11.0,47.0)),
                                              3.0+dot(p,float2(41.0,29.0)),
                                              4.0+dot(p,float2(23.0,31.0))))*103.0); }
float4 texNoTileTech2(sampler2D tex, float2 uv) {
	float2 iuv = floor(uv);
	float2 fuv = frac(uv);
	// Voronoi contribution
	float4 va = 0.0;
	float wt = 0.0;
    const float _BlendRatio  = 1.5;
	float blur = -(_BlendRatio ) * 30.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			float2 g = float2((float)i, (float)j);
			float4 o = hash4(iuv + g);
		    // Compute the blending weight proportional to a gaussian fallof
			float2 r = g - fuv + o.xy;
			float d = dot(r, r);
			float w = exp(blur * d);
			float4 c = tex2Dlod(tex,float4(frac(uv + o.zw), 0, 0));
			va += w * c;
			wt += w;
		}
	}

	// Normalization
	return va/wt;
}

            float4 TriPlanarSample(sampler2D tex, float3 uv, float3 normal)
            {
                #ifdef USE_VORONOI_SAMPLE
                float4 xyCol = texNoTileTech2(tex, uv.xy);
                float4 xzCol = texNoTileTech2(tex, uv.xz);
                float4 zyCol = texNoTileTech2(tex, uv.zy);
                #else
                float4 xyCol = tex2D(tex, uv.xy);
                float4 xzCol = tex2D(tex, uv.xz);
                float4 zyCol = tex2D(tex, uv.zy);
                #endif
                float xyWeight = normal.z;
                float xzWeight = normal.y;
                float zyWeight = normal.x;
                return (xyCol * xyWeight + xzCol * xzWeight + zyCol * zyWeight) / dot(float3(xyWeight, xzWeight, zyWeight), 1);
            }

            float frag (v2f i) : SV_TARGET
            {
                float3 localPos = i.objectPos;
                [branch]
                if(dot(abs(localPos.xyz) > 0.5, 1) > 0.5)
                {
                    discard;
                }
                localPos.xyz += 0.5;
                float maskValue = TriPlanarSample(_MaskTex, localPos.xyz, normalize(i.normal)).x;
                return _BlendVar * maskValue;
            }
            ENDCG
        }
    }
}
