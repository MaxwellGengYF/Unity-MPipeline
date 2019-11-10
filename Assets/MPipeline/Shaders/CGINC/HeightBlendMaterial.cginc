#ifndef __HEIGHTBLEND_INCLUDE__
#define __HEIGHTBLEND_INCLUDE__
struct HeightBlendMaterial
{
    float firstMaterialIndex;
    float secondMaterialIndex;
    float offset;
    float heightBlendScale;
};
Texture2DArray<float4> _MainTex; SamplerState sampler_MainTex;
Texture2DArray<float2> _BumpMap; SamplerState sampler_BumpMap;
Texture2DArray<float4> _SMMap; SamplerState sampler_SMMap;
Texture2DArray<float> _HeightMap; SamplerState sampler_HeightMap;
StructuredBuffer<HeightBlendMaterial> _MaterialBuffer;
Texture2D<float4> _NoiseTexture; SamplerState sampler_NoiseTexture;

float4 SampleNoTile(Texture2DArray<float4> tex, SamplerState samp, float2 x, float index, float v)
{
    float k = _NoiseTexture.SampleLevel(sampler_NoiseTexture, x * 0.05, 0).a;
    float l = k*8.0;
    float f = frac(l);
    float ia = floor(l); // iq method
    float ib = ia + 1.0;
    float2 offa = sin(float2(3.0,7.0)*ia); // can replace with any other hash
    float2 offb = sin(float2(3.0,7.0)*ib); // can replace with any other hash

    float4 cola = tex.SampleLevel(samp, float3(x + v*offa, index), 0);
    float4 colb = tex.SampleLevel(samp, float3(x + v*offb, index), 0);
    return lerp(cola, colb,  smoothstep(0.2,0.8,f-0.1*dot(cola-colb, 1)));
}

float2 SampleNoTileXY(Texture2DArray<float2> tex, SamplerState samp, float2 x, float index, float scale)
{
    const float v = 0.6;
    float k = _NoiseTexture.SampleLevel(sampler_NoiseTexture, x * 0.005, 0);
    float l = k*8.0;
    float f = frac(l);
    float ia = floor(l); // iq method
    float ib = ia + 1.0;
    float2 offa = sin(float2(3.0,7.0)*ia); // can replace with any other hash
    float2 offb = sin(float2(3.0,7.0)*ib); // can replace with any other hash

    float2 cola = tex.SampleLevel(samp, float3(x + v*offa, index), scale);
    float2 colb = tex.SampleLevel(samp, float3(x + v*offb, index), scale);
    return lerp(cola, colb,  smoothstep(0.2,0.8,f-0.1*dot(cola-colb, 1)));
}

void GetHeightBlendMaterial(float bufferIndex, float2 uv, float scale, out float4 albedo_occ, out float2 normal, out float2 sm)
{
    HeightBlendMaterial mat = _MaterialBuffer[bufferIndex];
    float firstHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.firstMaterialIndex), scale);
    float secondHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.secondMaterialIndex), scale);
    float blendWeight = saturate(mat.heightBlendScale * (firstHeight - secondHeight + mat.offset) * 0.5 + 0.5);
    
    albedo_occ = lerp(_MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.firstMaterialIndex), scale), _MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.secondMaterialIndex),scale), blendWeight);
    normal = lerp(_BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.firstMaterialIndex),scale), _BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.secondMaterialIndex), scale), blendWeight);
    sm = lerp(_SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.firstMaterialIndex), scale).xy, _SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.secondMaterialIndex), scale).xy, blendWeight);
   /* albedo_occ = lerp(SampleNoTile(_MainTex, sampler_MainTex, uv, mat.firstMaterialIndex, scale),SampleNoTile(_MainTex, sampler_MainTex, uv, mat.secondMaterialIndex,scale), blendWeight);
    normal = lerp(SampleNoTileXY(_BumpMap, sampler_BumpMap, uv, mat.firstMaterialIndex, scale),SampleNoTileXY(_BumpMap, sampler_BumpMap,  uv, mat.secondMaterialIndex, scale), blendWeight);
    sm = lerp(SampleNoTile(_SMMap, sampler_SMMap, uv, mat.firstMaterialIndex, scale).xy, SampleNoTile(_SMMap, sampler_SMMap, uv, mat.secondMaterialIndex, scale).xy, blendWeight);*/
}

void GetHeightBlendInEditor(HeightBlendMaterial mat, float3 albedo0, float3 normal0, float4 smoh0,float3 albedo1, float3 normal1, float4 smoh1, out float3 albedo, out float2 normal, out float3 smo)
{
    float blendWeight = saturate(mat.heightBlendScale * (smoh0.w - smoh1.w + mat.offset) * 0.5 + 0.5);
    albedo = lerp(albedo0, albedo1, blendWeight);
    normal = lerp(normal0, normal1, blendWeight).xy * 0.5 + 0.5;
    smo = lerp(smoh0, smoh1, blendWeight).xyz;
}

#endif