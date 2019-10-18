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

void GetHeightBlendMaterial(float bufferIndex, float2 uv, out float4 albedo_occ, out float2 normal, out float2 sm)
{
    HeightBlendMaterial mat = _MaterialBuffer[bufferIndex];
    float firstHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.firstMaterialIndex), 0);
    float secondHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.secondMaterialIndex), 0);
    float blendWeight = saturate(mat.heightBlendScale * (firstHeight - secondHeight + mat.offset) * 0.5 + 0.5);
    albedo_occ = lerp(_MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.firstMaterialIndex), 0), _MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.secondMaterialIndex), 0), blendWeight);
    normal = lerp(_BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.firstMaterialIndex), 0), _BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.secondMaterialIndex), 0), blendWeight);
    sm = lerp(_SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.firstMaterialIndex), 0).xy, _SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.secondMaterialIndex), 0).xy, blendWeight);
}

#endif