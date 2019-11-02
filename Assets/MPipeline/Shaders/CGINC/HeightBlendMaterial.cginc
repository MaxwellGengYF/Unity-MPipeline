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

void GetHeightBlendMaterial(float bufferIndex, float2 uv, float scale, out float4 albedo_occ, out float2 normal, out float2 sm)
{
    HeightBlendMaterial mat = _MaterialBuffer[bufferIndex];
    float firstHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.firstMaterialIndex), scale);
    float secondHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.secondMaterialIndex), scale);
    float blendWeight = saturate(mat.heightBlendScale * (firstHeight - secondHeight + mat.offset) * 0.5 + 0.5);
    albedo_occ = lerp(_MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.firstMaterialIndex), scale), _MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.secondMaterialIndex),scale), blendWeight);
    normal = lerp(_BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.firstMaterialIndex),scale), _BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.secondMaterialIndex), scale), blendWeight);
    sm = lerp(_SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.firstMaterialIndex), scale).xy, _SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.secondMaterialIndex), scale).xy, blendWeight);
}

void GetHeightBlendInEditor(HeightBlendMaterial mat, float3 albedo0, float3 normal0, float4 smoh0,float3 albedo1, float3 normal1, float4 smoh1, out float3 albedo, out float2 normal, out float3 smo)
{
    float blendWeight = saturate(mat.heightBlendScale * (smoh0.w - smoh1.w + mat.offset) * 0.5 + 0.5);
    albedo = lerp(albedo0, albedo1, blendWeight);
    normal = lerp(normal0, normal1, blendWeight).xy * 0.5 + 0.5;
    smo = lerp(smoh0, smoh1, blendWeight).xyz;
}

#endif