#ifndef __HEIGHTBLEND_INCLUDE__
#define __HEIGHTBLEND_INCLUDE__
struct HeightBlendMaterial
{
    float materialIndex;
    float3 albedoColor;
    float2 normalScale;
    float smoothness;
    float metallic;
    float occlusion;
    float antiRepeat;
};
Texture2DArray<float4> _MainTex; SamplerState sampler_MainTex;
Texture2DArray<float2> _BumpMap; SamplerState sampler_BumpMap;
Texture2DArray<float4> _SMMap; SamplerState sampler_SMMap;
Texture2DArray<float> _HeightMap; SamplerState sampler_HeightMap;
StructuredBuffer<HeightBlendMaterial> _MaterialBuffer;
Texture2D<float4> _NoiseTexture; SamplerState sampler_NoiseTexture;
Texture2D<float4> _NoiseTillingTexture; SamplerState sampler_NoiseTillingTexture;
float4 _Offset;//UV Integer Offset

void GetHeightBlendMaterial(float bufferIndex, float2 uv, float scale, float2 noiseValue, out float4 albedo_occ, out float2 normal, out float2 sm)
{
    HeightBlendMaterial mat = _MaterialBuffer[bufferIndex];
    [branch]
    if(mat.antiRepeat > 0.5){
        float2 absoluteUV = (_Offset.xy + uv) * 4;
        [flatten]
        if(abs(absoluteUV.y) % 2 >= 1)
        {
            absoluteUV.x += 0.5;
        }
        absoluteUV += noiseValue * 4;
        absoluteUV *= 0.00390625;
        float4 uvOffsetScale = _NoiseTillingTexture.SampleLevel(sampler_NoiseTillingTexture, absoluteUV, 0);
        uv = uv * uvOffsetScale.zw + uvOffsetScale.xy;
    }

    albedo_occ = _MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.materialIndex), scale);
    albedo_occ.xyz *= mat.albedoColor;
    albedo_occ.w = lerp(1, albedo_occ.w, mat.occlusion);
    normal = _BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.materialIndex),scale) * mat.normalScale;
    sm = _SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.materialIndex), scale).xy * float2(mat.smoothness, mat.metallic);
    /*
    float firstHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.firstMaterialIndex), scale);
    float secondHeight = _HeightMap.SampleLevel(sampler_HeightMap, float3(uv, mat.secondMaterialIndex), scale);
    float blendWeight = saturate(mat.heightBlendScale * (firstHeight - secondHeight + mat.offset) * 0.5 + 0.5);
    
    albedo_occ = lerp(_MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.firstMaterialIndex), scale), _MainTex.SampleLevel(sampler_MainTex, float3(uv, mat.secondMaterialIndex),scale), blendWeight);
    normal = lerp(_BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.firstMaterialIndex),scale), _BumpMap.SampleLevel(sampler_BumpMap, float3(uv, mat.secondMaterialIndex), scale), blendWeight);
    sm = lerp(_SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.firstMaterialIndex), scale).xy, _SMMap.SampleLevel(sampler_SMMap, float3(uv, mat.secondMaterialIndex), scale).xy, blendWeight);*/
}
#endif