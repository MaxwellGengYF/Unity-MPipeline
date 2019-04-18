#ifndef __VOXELLIGHT_INCLUDE__
#define __VOXELLIGHT_INCLUDE__

#define XRES 32
#define YRES 16
#define ZRES 128
#define VOXELZ 64
#define MAXLIGHTPERCLUSTER 128
#define FROXELRATE 1.25
#define CLUSTERRATE 1.5
static const uint3 _ScreenSize = uint3(160, 90, 128);
#include "CGINC/Plane.cginc"

#define VOXELSIZE uint3(XRES, YRES, ZRES)


            struct PointLight{
                float3 lightColor;
                float4 sphere;
                int shadowIndex;
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
            };

            struct FogVolume
            {
                float3x3 localToWorld;
                float4x4 worldToLocal;
                float3 position;
                float3 extent;
                float targetVolume;
                float3 color;
                float3 emissionColor;
            };
            struct AreaLight
            {
                float4x4 mat;
                float3 color;
            };
float3 _CameraForward;
float3 _CameraNearPos;
float3 _CameraFarPos;
float3 _NearFarClip; //x: farClip / availiable distance y: nearclip / availiable distance z: nearClip

inline uint GetIndex(uint3 id, const uint3 size, const int multiply){
    const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
    return dot(id, multiValue);
}

#endif