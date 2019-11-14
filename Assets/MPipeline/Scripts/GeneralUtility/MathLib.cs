using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe static class MathLib
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 toMatrix4x4(ref this double4x4 db)
        {
            return new Matrix4x4((float4)db.c0, (float4)db.c1, (float4)db.c2, (float4)db.c3);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double4x4 toDouble4x4(ref this Matrix4x4 db)
        {
            return new double4x4((float4)db.GetColumn(0), (float4)db.GetColumn(1), (float4)db.GetColumn(2), (float4)db.GetColumn(3));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 GetWorldToLocal(ref float4x4 localToWorld)
        {
            float4x4 rotation = float4x4(float4(localToWorld.c0.xyz, 0), float4(localToWorld.c1.xyz, 0), float4(localToWorld.c2.xyz, 0), float4(0, 0, 0, 1));
            rotation = transpose(rotation);
            float3 localPos = mul(rotation, localToWorld.c3).xyz;
            localPos = -localPos;
            rotation.c3 = float4(localPos.xyz, 1);
            return rotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetPlane(float3 normal, float3 inPoint)
        {
            return new float4(normal, -dot(normal, inPoint));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double4 GetPlaneDouble(double3 normal, double3 inPoint)
        {
            return new double4(normal, -dot(normal, inPoint));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetPlane(float3 a, float3 b, float3 c)
        {
            float3 normal = normalize(cross(b - a, c - a));
            return float4(normal, -dot(normal, a));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double4 GetPlaneDouble(double3 a, double3 b, double3 c)
        {
            double3 normal = normalize(cross(b - a, c - a));
            return double4(normal, -dot(normal, a));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetDistanceToPlane(float4 plane, float3 inPoint)
        {
            return dot(plane.xyz, inPoint) + plane.w;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 CameraSpacePlane(float3x4 worldToCameraMatrix, float3 pos, float3 normal)
        {
            float3 cpos = mul(worldToCameraMatrix, float4(pos, 1));
            float3 cnormal = mul(worldToCameraMatrix, float4(normalize(normal), 0));
            return float4(cnormal, -dot(cpos, cnormal));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetSphereRandom(float2 random)
        {
            double phi = 2 * PI * random.x;
            double cosTheta = 1 - 2 * random.y;
            double sinTheta = sqrt(1 - cosTheta * cosTheta);
            return (float3)double3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBits32(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
            bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
            bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
            bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
            return bits;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Hammersley(uint Index, uint NumSamples)
        {
            return float2((float)Index / (float)NumSamples, (float)((double)ReverseBits32(Index) / 0xffffffffu));
        }

        public static bool BoxIntersect(float3 position, float3 extent, float4* planes, int len)
        {
            for (uint i = 0; i < len; ++i)
            {
                float4 plane = planes[i];
                float3 absNormal = abs(plane.xyz);
                if ((dot(position, plane.xyz) - dot(absNormal, extent)) > -plane.w)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool BoxIntersect(double3 position, double3 extent, float4* planes, int len)
        {
            for (uint i = 0; i < len; ++i)
            {
                float4 plane = planes[i];
                float3 absNormal = abs(plane.xyz);
                if ((dot(position, plane.xyz) - dot(absNormal, extent)) > -plane.w)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool BoxContactWithBox(double3 min0, double3 max0, double3 min1, double3 max1)
        {
            bool3 v = min0 > max1;
            if (v.x || v.y || v.z) return false;
            v = min1 > max0;
            if (v.x || v.y || v.z) return false;
            return true;
        }

        public static bool BoxIntersect(ref float4x4 localToWorld, float3 position, float3 extent, float4* planes, int len)
        {
            position = mul(localToWorld, float4(position, 1)).xyz;
            for (uint i = 0; i < len; ++i)
            {
                float4 plane = planes[i];
                float3 absNormal = abs(mul(plane.xyz, float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz)));
                if ((dot(position, plane.xyz) - dot(absNormal, extent)) > -plane.w)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool BoxIntersect(float3x3 boxLocalToWorld, float3 position, float4* planes, int len)
        {
            for (uint i = 0; i < len; ++i)
            {
                float4 plane = planes[i];
                float3 absNormal = abs(mul(plane.xyz, boxLocalToWorld));
                if ((dot(position, plane.xyz) - dot(absNormal, float3(0.5f, 0.5f, 0.5f))) > -plane.w) return false;
            }
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointInsidePlane(float3 vertex, float4 plane)
        {
            return (dot(plane.xyz, vertex) + plane.w) < 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ConeIntersect(Cone cone, float4 plane)
        {
            float3 m = cross(cross(plane.xyz, cone.direction), cone.direction);
            float3 Q = cone.vertex + cone.direction * cone.height + normalize(m) * cone.radius;
            return PointInsidePlane(cone.vertex, plane) || PointInsidePlane(Q, plane);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SphereIntersect(float4 sphere, float4 plane)
        {
            return (GetDistanceToPlane(plane, sphere.xyz) < sphere.w);
        }

        public static void Quicksort<T>(T* a, int p, int q) where T : unmanaged, IFunction<T, int>
        {
            int i = p;
            int j = q;
            T temp = a[p];

            while (i < j)
            {
                while (a[j].Run(ref temp) >= 0 && j > i) j--;

                if (j > i)
                {
                    a[i] = a[j];
                    i++;
                    while (a[i].Run(ref temp) <= 0 && i < j) i++;
                    if (i < j)
                    {
                        a[j] = a[i];
                        j--;
                    }
                }
            }
            a[i] = temp;
            if (p < (i - 1)) Quicksort(a, p, i - 1);
            if ((j + 1) < q) Quicksort(a, j + 1, q);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3x4 GetLocalToWorld(Transform trans)
        {
            float4x4 fx = trans.localToWorldMatrix;
            return new float3x4(fx.c0.xyz, fx.c1.xyz, fx.c2.xyz, fx.c3.xyz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DistanceToQuad(double size, double2 quadToTarget)
        {
            quadToTarget = abs(quadToTarget);
            double len = length(quadToTarget);
            quadToTarget /= len;
            double dotV = max(dot(double2(0, 1), quadToTarget), dot(double2(1, 0), quadToTarget));
            double leftLen = size / dotV;
            return len - leftLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DistanceToCube(double3 size, double3 quadToTarget)
        {
            quadToTarget = abs(quadToTarget);
            double len = length(quadToTarget);
            quadToTarget /= len;
            double dotV = min(size.x / quadToTarget.x, size.y / quadToTarget.y);
            dotV = min(dotV, size.z / quadToTarget.z);
            return len - dotV;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3x4 GetWorldToLocal(Transform trans)
        {
            float4x4 fx = trans.worldToLocalMatrix;
            return new float3x4(fx.c0.xyz, fx.c1.xyz, fx.c2.xyz, fx.c3.xyz);
        }
    }
}