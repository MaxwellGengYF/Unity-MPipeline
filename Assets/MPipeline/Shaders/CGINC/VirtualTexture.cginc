#ifndef VIRTUAL_TEXTURE
#define VIRTUAL_TEXTURE
Texture2D<half4> _IndexTexture;
SamplerState sampler_IndexTexture;
float4 _IndexTexture_TexelSize;
inline float3 GetVirtualTextureUV(float2 startChunkPos, float2 localUV);
///Sample Virtual TExture
///tex: target texture
/// targetChunk: absolute index in indextex(adaptive)
///localUV: 0-1 uv
inline float4 SampleVirtualTexture(Texture2DArray<float4> tex, SamplerState samp, float2 targetChunk, float2 localUV)
{
    return tex.Sample(samp, GetVirtualTextureUV(targetChunk, localUV));
}

/// startChunkPos: absolute index in indextex(adaptive)
///localUV: 0-1 uv
inline float3 GetVirtualTextureUV(float2 startChunkPos, float2 localUV)
{
    startChunkPos = (0.25 + startChunkPos) % _IndexTexture_TexelSize.zw;
    float4 scaleOffset = _IndexTexture[startChunkPos];
    scaleOffset.w *= 2048;
    localUV = localUV * scaleOffset.x + scaleOffset.yz;
    return float3(localUV, scaleOffset.w);
}
#endif