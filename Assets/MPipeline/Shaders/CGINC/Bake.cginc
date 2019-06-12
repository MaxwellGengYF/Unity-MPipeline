#ifndef MPIP_BAKE_CGINC
#define MPIP_BAKE_CGINC

float4x4 _Bake_VP;

struct a2v {
	float4 vert : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
};

struct v2f {
	float4 pos : SV_POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
	float4 worldPos : TEXCOORD1;
};

v2f vert_bake_light_probe(a2v i) {
	v2f o;
	o.pos = mul(_Bake_VP, mul(unity_ObjectToWorld, i.vert));
	//o.pos.x *= -1;
	//o.pos = UnityObjectToClipPos(i.vert);
	o.normal = UnityObjectToWorldNormal(i.normal);
	o.worldPos = mul(unity_ObjectToWorld, i.vert);
	o.uv = i.uv;
	return o;
}


float3 frag_bake_light_probe(v2f i) : SV_TARGET{

	return tex2D(_MainTex, i.uv)* _Color.rgb;
}

#endif