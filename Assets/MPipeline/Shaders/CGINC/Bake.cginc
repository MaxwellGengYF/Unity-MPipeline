#ifndef MPIP_BAKE_CGINC
#define MPIP_BAKE_CGINC

float4x4 _Bake_VP;
float3 _Bake_ProbePosition;

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


void frag_bake_light_probe(v2f i, out float4 out0 : SV_TARGET0, out float4 out1 : SV_TARGET1){
	float3 normal = i.normal;
	normal = normalize(normal);
	out0 = float4(tex2D(_MainTex, i.uv).rgb * _Color.rgb, normal.x);
	out1 = float4(normal.yz, length(i.worldPos.xyz - _Bake_ProbePosition), 1);
}

#endif