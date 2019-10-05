#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;

namespace MPipeline.PCG
{
    public struct MeshAppdata
    {
        public float3 vertex;
        public float3 normal;
        public float4 tangent;
        public float2 uv;
    };

    public struct PerObjectDatas
    {
        public float4x4 localToWorldMatrix;
        public float4 uvTileOffset;
    };
    public struct TriangleData
    {
        public MeshAppdata v0;
        public MeshAppdata v1;
        public MeshAppdata v2;
    };

    public struct RayIntersectResult
    {
        public TriangleData triangle;
        public float t;
        public float2 uv;
    }

    public unsafe static class PCGLibrary
    {

        public static MeshAppdata GetPointAtTriangle(float2 uv, TriangleData data)
        {
            MeshAppdata result;
            float minus = 1 - uv.x - uv.y;
            result.vertex = minus * data.v0.vertex + uv.x * data.v1.vertex + uv.y * data.v2.vertex;
            result.normal = minus * data.v0.normal + uv.x * data.v1.normal + uv.y * data.v2.normal;
            result.tangent = minus * data.v0.tangent + uv.x * data.v1.tangent + uv.y * data.v2.tangent;
            result.uv = minus * data.v0.uv + uv.x * data.v1.uv + uv.y * data.v2.uv;
            return result;
        }

        public static bool IntersectTriangle(float3 orig, float3 dir, float3 v0, float3 v1, float3 v2
        , float* t, float2* uv)
        {
            float3 e1 = v1 - v0;
            float3 e2 = v2 - v0;
            float3 p = cross(dir, e2);
            float det = dot(e1, p);
            float3 T;
            if (det > 0)
            {
                T = orig - v0;
            }
            else
            {
                T = v0 - orig;
                det = -det;
            }
            if (det < 0.0001) return false;
            uv->x = dot(T, p);
            if (uv->x < 0.0f || uv->x > det) return false;
            float3 Q = cross(T, e1);
            uv->y = dot(dir, Q);
            if (uv->y < 0.0f || (uv->x + uv->y) > det) return false;
            *t = dot(e2, Q);
            float fInvDet = 1 / det;
            *t *= fInvDet;
            *uv *= fInvDet;
            return true;
        }
        [Unity.Burst.BurstCompile]
        private struct RayIntersectJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public int* triangles;
            [NativeDisableUnsafePtrRestriction]
            public MeshAppdata* appdatas;
            public float3 origin;
            public float3 dir;
            [NativeDisableUnsafePtrRestriction]
            public float4x4* transformMatrix;
            public NativeList<RayIntersectResult> results;
            public void Execute(int index)
            {
                index *= 3;
                MeshAppdata v0 = appdatas[triangles[index]];
                MeshAppdata v1 = appdatas[triangles[index + 1]];
                MeshAppdata v2 = appdatas[triangles[index + 2]];
                float t = float.MaxValue;
                float2 uv = 0;

                if (IntersectTriangle(origin, dir, mul(*transformMatrix, float4(v0.vertex, 1)).xyz, mul(*transformMatrix, float4(v1.vertex, 1)).xyz, mul(*transformMatrix, float4(v2.vertex, 1)).xyz, &t, &uv) && t > 0)
                {
                    results.ConcurrentAdd(new RayIntersectResult
                    {
                        t = t,
                        triangle = new TriangleData
                        {
                            v0 = v0,
                            v1 = v1,
                            v2 = v2
                        },
                        uv = uv
                    });
                }
            }
        }
        public static bool RayIntersectToMesh(int* triangles, int triangleCount, MeshAppdata* appdatas, float3 origin, float3 dir, float4x4* transformMatrix, out RayIntersectResult result)
        {
            NativeList<RayIntersectResult> allResults = new NativeList<RayIntersectResult>(triangleCount, Allocator.Temp);
            RayIntersectJob jb = new RayIntersectJob
            {
                appdatas = appdatas,
                dir = dir,
                origin = origin,
                results = allResults,
                triangles = triangles,
                transformMatrix = transformMatrix
            };

            jb.Schedule(triangleCount, max(1, triangleCount / 10)).Complete();
            if (allResults.Length <= 0)
            {
                result = new RayIntersectResult();
                return false;
            }
            float minT = allResults[0].t;
            int index = 0;
            for (int i = 1; i < allResults.Length; ++i)
            {
                if (minT > allResults[i].t)
                {
                    index = i;
                }
            }
            result = allResults[index];
            return true;
        }
        public static NativeList<RayIntersectResult> RayIntersectToMesh(int* triangles, int triangleCount, MeshAppdata* appdatas, float3 origin, float3 dir, float4x4* transformMatrix, Allocator alloc)
        {
            NativeList<RayIntersectResult> allResults = new NativeList<RayIntersectResult>(triangleCount, alloc);
            RayIntersectJob jb = new RayIntersectJob
            {
                appdatas = appdatas,
                dir = dir,
                origin = origin,
                results = allResults,
                triangles = triangles,
                transformMatrix = transformMatrix
            };
            jb.Schedule(triangleCount, max(1, triangleCount / 10)).Complete();
            return allResults;
        }
        public static NativeList<float4> GenerateHolePlane(float2 size, NativeArray<float> rowVerticlePoses, NativeArray<float> colHorizonPoses, Allocator alloc = Allocator.Persistent)
        {
            float2 holeExtent = size / 2f;
            int sumCount = colHorizonPoses.Length + 1 + (rowVerticlePoses.Length + 1) * colHorizonPoses.Length;
            NativeList<float4> results = new NativeList<float4>(sumCount, Allocator.Persistent);
            //Add Verticle Datas
            float leftCorner = 0;
            for (int i = 0; i < colHorizonPoses.Length; ++i)
            {
                float rightPos = colHorizonPoses[i] - size.x;
                results.Add(float4(leftCorner, 0, rightPos, 1));
                leftCorner = colHorizonPoses[i] + size.x;
            }
            results.Add(float4(leftCorner, 0, 1, 1));
            //Add Horizontal Datas
            for (int x = 0; x < colHorizonPoses.Length; ++x)
            {
                float upCorner = 0;
                float leftStep = colHorizonPoses[x] - size.x;
                float rightStep = colHorizonPoses[x] + size.x;
                for (int y = 0; y < rowVerticlePoses.Length; ++y)
                {
                    float upStep = rowVerticlePoses[y] - size.y;
                    results.Add(float4(leftStep, upCorner, rightStep, upStep));
                    upCorner = rowVerticlePoses[y] + size.y;
                }
                results.Add(float4(leftStep, upCorner, rightStep, 1));
            }
            return results;
        }

        public static void GetPointsWithArrays(NativeList<Point> points, NativeList<int> materialPoints, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, Vector4[] tangents, int[] triangles, int materialCount)
        {
            void PointSet(int i)
            {
                float4 tan = tangents[i];
                float3 nor = normals[i];
                points.Add(new Point
                {
                    vertex = vertices[i],
                    tangent = tan,
                    normal = nor,
                    uv0 = uvs[i],
                });
            }
            for (int index = 0; index < triangles.Length; index += 3)
            {
                PointSet(triangles[index]);
                PointSet(triangles[index + 1]);
                PointSet(triangles[index + 2]);
                materialPoints.Add(materialCount);
            }
        }
        public static Mesh GeneratePlaneMeshFromCoord(NativeList<float4> coords, float4x4 localToModelMatrix, float2 startUV, float2 endUV)
        {
            List<Vector3> allVertices = new List<Vector3>(coords.Length * 4);
            List<Vector2> uvs = new List<Vector2>(allVertices.Capacity);
            List<Vector3> normals = new List<Vector3>(allVertices.Capacity);
            List<Vector4> tans = new List<Vector4>(allVertices.Capacity);
            List<int> triangles = new List<int>(allVertices.Capacity * 3 / 2 + 1);
            void AddPoint(float2 leftUpCorner, float2 rightDownCorner)
            {
                int* triangleCount = stackalloc int[] { 0, 1, 2, 1, 3, 2 };
                float2* verts = stackalloc float2[] { leftUpCorner, float2(rightDownCorner.x, leftUpCorner.y), float2(leftUpCorner.x, rightDownCorner.y), rightDownCorner };
                int len = allVertices.Count;
                for (int i = 0; i < 6; ++i)
                {
                    triangles.Add(triangleCount[i] + len);
                }
                for (int i = 0; i < 4; ++i)
                {
                    allVertices.Add(mul(localToModelMatrix, float4(verts[i], 0, 1)).xyz);
                    uvs.Add(lerp(startUV, endUV, verts[i]));
                    normals.Add(mul(localToModelMatrix, float4(0, 0, 1, 0)).xyz);
                    tans.Add(float4(mul(localToModelMatrix, float4(1, 0, 0, 0)).xyz, 1));
                }
            }
            Mesh m = new Mesh();
            foreach (var i in coords)
            {
                AddPoint(i.xy, i.zw);
            }
            m.SetVertices(allVertices);
            m.SetUVs(0, uvs);
            m.SetTangents(tans);
            m.SetNormals(normals);
            m.SetTriangles(triangles, 0);
            return m;
        }
        /// <summary>
        /// Generate Native Appdata from mesh
        /// </summary>
        /// <param name="appdatas"></param>
        /// <param name="triangles"></param>
        /// <returns></returns> vertex Count
        public static int GetAppDataFromMesh(Mesh mesh, out NativeArray<MeshAppdata> meshData, out int[] triangles)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Vector4[] tan = mesh.tangents;
            Vector3[] normal = mesh.normals;
            if (uv.Length != vertices.Length)
            {
                uv = new Vector2[vertices.Length];
            }
            if (tan.Length != vertices.Length)
            {
                tan = new Vector4[vertices.Length];
            }
            if (normal.Length != vertices.Length)
            {
                normal = new Vector3[vertices.Length];
            }
            if (uv.Length != vertices.Length)
            {
                uv = new Vector2[vertices.Length];
            }
            meshData = new NativeArray<MeshAppdata>(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < meshData.Length; ++i)
            {
                meshData[i] = new MeshAppdata
                {
                    vertex = vertices[i],
                    normal = normal[i],
                    tangent = tan[i],
                    uv = uv[i]
                };
            }
            triangles = mesh.triangles;
            return vertices.Length;
        }
        public static void GetTransformedMeshData(Mesh targetMesh, Matrix4x4 transformMat, out Vector3[] vertices, out Vector4[] tangents, out Vector2[] uvs, out Vector3[] normals)
        {
            vertices = targetMesh.vertices;
            normals = targetMesh.normals;
            uvs = targetMesh.uv;
            tangents = targetMesh.tangents;

            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i] = transformMat.MultiplyPoint(vertices[i]);
                ///TODO
                ///Add others
            }
            if (normals.Length == vertices.Length)
            {
                for (int i = 0; i < vertices.Length; ++i)
                {
                    normals[i] = transformMat.MultiplyVector(normals[i]);
                }
            }
            else
            {
                normals = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; ++i)
                {
                    normals[i] = new Vector3(0, 0, 1);
                }
            }

            if (uvs.Length != vertices.Length)
            {
                uvs = new Vector2[vertices.Length];
            }
            if (tangents.Length == vertices.Length)
            {
                for (int i = 0; i < vertices.Length; ++i)
                {
                    float4 tan = tangents[i];
                    tan.xyz = transformMat.MultiplyVector(tan.xyz);
                    tangents[i] = tan;
                }
            }
            else
            {
                tangents = new Vector4[vertices.Length];
                for (int i = 0; i < vertices.Length; ++i)
                {
                    tangents[i] = new float4(transformMat.MultiplyVector(new Vector3(0, 0, 1)), 1);
                }
            }
        }
    }
}
#endif