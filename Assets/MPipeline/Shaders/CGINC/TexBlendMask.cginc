#ifndef TEX_BLEND_MASK_INCLUDE
#define TEX_BLEND_MASK_INCLUDE

sampler2D _BlendAlbedo;
sampler2D _BlendNormal;
sampler2D _BlendSMO;
sampler2D _BlendMask;
float4 _BlendScaleOffset;
float2 _MaskScaleOffset;
float3 ProcessNormal(float4 value)
{
    value.x *= value.w > 0.01 ? value.w : 1;
    float3 n;
    n.xy = value.xy * 2 - 1;
    n.z = sqrt(1 - dot(n.xy, n.xy));
    return n;
}

void GetBeforeBlendResult(float2 uv, out float3 albedo, out float3 normal, out float3 smo, out float mask)
{
    float2 originUV = uv;
    uv = uv * _BlendScaleOffset.xy + _BlendScaleOffset.zw;
        albedo = tex2D(_BlendAlbedo, uv).xyz;
        normal = ProcessNormal(tex2D(_BlendNormal, uv));
        smo = tex2D(_BlendSMO, uv).xyz;
    
    mask = saturate(tex2D(_BlendMask, originUV).x * _MaskScaleOffset.x + _MaskScaleOffset.y);
}

#endif