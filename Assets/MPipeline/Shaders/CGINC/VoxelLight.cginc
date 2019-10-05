#ifndef __VOXELLIGHT_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __VOXELLIGHT_INCLUDE__

#define XRES 32
#define YRES 16
#define ZRES 64
#define VOXELZ 64
#define MAXLIGHTPERCLUSTER 128
#define FROXELRATE 1.35
#define CLUSTERRATE 1.5
float3 _FroxelSize;
#include "CGINC/Plane.cginc"

#define VOXELSIZE uint3(XRES, YRES, ZRES)


            struct PointLight{
                float3 lightColor;
                float4 sphere;
                int shadowIndex;
                float shadowBias;
            };
            struct SpotLight
            {
                float3 lightColor;
                Cone lightCone;
                float angle;
                float4x4 vpMatrix;
                float smallAngle;
                float nearClip;
                int shadowIndex;
                int iesIndex;
                float shadowBias;
            };

            struct FogVolume
            {
                float3x3 localToWorld;
                float3x4 worldToLocal;
                float3 position;
                float3 extent;
                float targetVolume;
                float3 color;
                float3 emissionColor;
            };
            struct Decal
            {
                float3x4 localToWorldMat;
                float3x4 worldToLocal;
                float3 minPosition;
                float3 maxPosition;
                float4 albedoScaleOffset;
                float4 normalScaleOffset;
                float4 specularScaleOffset;
                int3 texIndex;
                uint layer;
                float2 heightmapScaleOffset;
                float3 opacity;
            };
float3 _CameraForward;
float4 _CameraNearPos;
float4 _CameraFarPos;
float4 _VolumetricLightVar; //x: Camera nearclip plane      y: Volume distance - nearclip       z: volume distance      w: indirect intensity

inline uint GetIndex(uint3 id, const uint3 size, const int multiply){
    const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
    return dot(id, multiValue);
}

#endif