using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public struct Vector4Int
    {
        public int x;
        public int y;
        public int z;
        public int w;
        public Vector4Int(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    public struct Point
    {
        public float3 position;
    }

    public interface IFunction<R>
    {
        R Run();
    }

    public interface IFunction<A, R>
    {
        R Run(ref A a);
    }

    public interface IFunction<A, B, R>
    {
        R Run(ref A a, ref B b);
    }

    public interface IFunction<A, B, C, R>
    {
        R Run(ref A a, ref B b, ref C c);
    }

    public interface IFunction<A, B, C, D, R>
    {
        R Run(ref A a, ref B b, ref C c, ref D d);
    }

    public interface IFunction<A, B, C, D, E, R>
    {
        R Run(ref A a, ref B b, ref C c, ref D d, ref E e);
    }

    public interface IFunction<A, B, C, D, E, F, R>
    {
        R Run(ref A a, ref B b, ref C c, ref D d, ref E e, ref F f);
    }

    public interface IFunction<A, B, C, D, E, F, G, R>
    {
        R Run(ref A a, ref B b, ref C c, ref D d, ref E e, ref F f, ref G g);
    }

    public interface IFunction<A, B, C, D, E, F, G, H, R>
    {
        R Run(ref A a, ref B b, ref C c, ref D d, ref E e, ref F f, ref G g, ref H h);
    }

    public interface IAction
    {
        void Run();
    }
    public interface IAction<A>
    {
        void Run(ref A a);
    }
    public interface IAction<A, B>
    {
        void Run(ref A a, ref B b);
    }
    public interface IAction<A, B, C>
    {
        void Run(ref A a, ref B b, ref C c);
    }
    public interface IAction<A, B, C, D>
    {
        void Run(ref A a, ref B b, ref C c, ref D d);
    }

    public interface IAction<A, B, C, D, E>
    {
        void Run(ref A a, ref B b, ref C c, ref D d, ref E e);
    }

    public interface IAction<A, B, C, D, E, F>
    {
        void Run(ref A a, ref B b, ref C c, ref D d, ref E e, ref F f);
    }

    public interface IAction<A, B, C, D, E, F, G>
    {
        void Run(ref A a, ref B b, ref C c, ref D d, ref E e, ref F f, ref G g);
    }


    public interface IAction<A, B, C, D, E, F, G, H>
    {
        void Run(ref A a, ref B b, ref C c, ref D d, ref E e, ref F f, ref G g, ref H h);
    }

    public unsafe struct DecalData
    {
        public float4x4 rotation;
        public float3 position;
        public void* comp;
    }

    public struct PointLightStruct
    {
        public float3 lightColor;
        public float4 sphere;
        public int shadowIndex;
    }
    public struct Cone
    {
        public float3 vertex;
        public float height;
        public float3 direction;
        public float radius;
        public Cone(float3 position, float distance, float3 direction, float angle)
        {
            vertex = position;
            height = distance;
            this.direction = direction;
            radius = math.tan(angle) * height;
        }
    }
    public struct Capsule
    {
        public float3 direction;
        public float3 position;
        public float radius;
    }
    public struct SpotLight
    {
        public float3 lightColor;
        public Cone lightCone;
        public float angle;
        public Matrix4x4 vpMatrix;
        public float smallAngle;
        public float nearClip;
        public int shadowIndex;
        public int iesIndex;
    };
    public unsafe struct CubemapViewProjMatrix
    {
        public int2 index;
        public void* mLightPtr;
        public Matrix4x4 forwardProjView;
        public Matrix4x4 backProjView;
        public Matrix4x4 upProjView;
        public Matrix4x4 downProjView;
        public Matrix4x4 rightProjView;
        public Matrix4x4 leftProjView;
        public float4* frustumPlanes;
    }

    public struct FogVolume
    {
        public float3x3 localToWorld;
        public float4x4 worldToLocal;
        public float3 position;
        public float3 extent;
        public float targetVolume;
        public float3 color;
        public float3 emissionColor;
    }

    public struct ReflectionData
    {
        public float3 position;
        public float3 minExtent;
        public float3 maxExtent;
        public float4 hdr;
        public float blendDistance;
        public int boxProjection;
    }

    public class PipelineBaseBuffer
    {
        public ComputeBuffer clusterBuffer;         //ObjectInfo
        public ComputeBuffer instanceCountBuffer;   //uint
        public ComputeBuffer dispatchBuffer;
        public ComputeBuffer reCheckResult;
        public ComputeBuffer resultBuffer;          //uint
        public ComputeBuffer verticesBuffer;        //Point
        public ComputeBuffer reCheckCount;        //Point
        public ComputeBuffer moveCountBuffer;
        public int clusterCount;
        public const int INDIRECTSIZE = 20;
        public const int CLUSTERCLIPCOUNT = 255;
        public const int CLUSTERVERTEXCOUNT = CLUSTERCLIPCOUNT;

        public const int ClusterCull_Kernel = 0;
        public const int ClearCluster_Kernel = 1;
        public const int UnsafeCull_Kernel = 2;
        public const int MoveVertex_Kernel = 3;
        public const int MoveCluster_Kernel = 4;
        public const int FrustumFilter_Kernel = 5;
        public const int OcclusionRecheck_Kernel = 6;
        public const int ClearOcclusionData_Kernel = 7;
    }

    [System.Serializable]
    public unsafe struct Matrix3x4
    {
        public float m00;
        public float m10;
        public float m20;
        public float m01;
        public float m11;
        public float m21;
        public float m02;
        public float m12;
        public float m22;
        public float m03;
        public float m13;
        public float m23;
        public const int SIZE = 48;
        public Matrix3x4(Matrix4x4 target)
        {
            m00 = target.m00;
            m01 = target.m01;
            m02 = target.m02;
            m03 = target.m03;
            m10 = target.m10;
            m11 = target.m11;
            m12 = target.m12;
            m13 = target.m13;
            m20 = target.m20;
            m21 = target.m21;
            m22 = target.m22;
            m23 = target.m23;
        }
        public Matrix3x4(Matrix4x4* target)
        {
            m00 = target->m00;
            m01 = target->m01;
            m02 = target->m02;
            m03 = target->m03;
            m10 = target->m10;
            m11 = target->m11;
            m12 = target->m12;
            m13 = target->m13;
            m20 = target->m20;
            m21 = target->m21;
            m22 = target->m22;
            m23 = target->m23;
        }
        public Matrix3x4(ref Matrix4x4 target)
        {
            m00 = target.m00;
            m01 = target.m01;
            m02 = target.m02;
            m03 = target.m03;
            m10 = target.m10;
            m11 = target.m11;
            m12 = target.m12;
            m13 = target.m13;
            m20 = target.m20;
            m21 = target.m21;
            m22 = target.m22;
            m23 = target.m23;
        }
    }

    public struct AspectInfo
    {
        public Vector3 inPlanePoint;
        public Vector3 planeNormal;
        public float size;
    }
    [System.Serializable]
    public struct CullBox
    {
        public Vector3 extent;
        public Vector3 position;
    }
    public struct Cluster
    {
        public Vector3 extent;
        public Vector3 position;
        public int index;
    }
    public struct PerObjectData
    {
        public Vector3 extent;
        public uint instanceOffset;
    }

    public struct PerspCam
    {
        public float3 right;
        public float3 up;
        public float3 forward;
        public float3 position;
        public float fov;
        public float nearClipPlane;
        public float farClipPlane;
        public float aspect;
        public float4x4 localToWorldMatrix;
        public float4x4 worldToCameraMatrix;
        public float4x4 projectionMatrix;
        public void UpdateTRSMatrix()
        {
            localToWorldMatrix.c0 = float4(right, 0);
            localToWorldMatrix.c1 = float4(up, 0);
            localToWorldMatrix.c2 = float4(forward, 0);
            localToWorldMatrix.c3 = float4(position, 1);
            worldToCameraMatrix = MatrixUtility.GetWorldToLocal(localToWorldMatrix);
            float4 row2 = -float4(worldToCameraMatrix.c0.z, worldToCameraMatrix.c1.z, worldToCameraMatrix.c2.z, worldToCameraMatrix.c3.z);
            worldToCameraMatrix.c0.z = row2.x;
            worldToCameraMatrix.c1.z = row2.y;
            worldToCameraMatrix.c2.z = row2.z;
            worldToCameraMatrix.c3.z = row2.w;
        }
        public void UpdateViewMatrix(float4x4 localToWorld)
        {
            worldToCameraMatrix = MatrixUtility.GetWorldToLocal(localToWorld);
            float4 row2 = -float4(worldToCameraMatrix.c0.z, worldToCameraMatrix.c1.z, worldToCameraMatrix.c2.z, worldToCameraMatrix.c3.z);
            worldToCameraMatrix.c0.z = row2.x;
            worldToCameraMatrix.c1.z = row2.y;
            worldToCameraMatrix.c2.z = row2.z;
            worldToCameraMatrix.c3.z = row2.w;
        }
        public void UpdateProjectionMatrix()
        {
            projectionMatrix = Matrix4x4.Perspective(fov, aspect, nearClipPlane, farClipPlane);
        }
    }

    public struct OrthoCam
    {
        public float4x4 worldToCameraMatrix;
        public float4x4 localToWorldMatrix;
        public float3 right;
        public float3 up;
        public float3 forward;
        public float3 position;
        public float size;
        public float nearClipPlane;
        public float farClipPlane;
        public float4x4 projectionMatrix;
        public void UpdateTRSMatrix()
        {
            localToWorldMatrix.c0 = new float4(right, 0);
            localToWorldMatrix.c1 = new float4(up, 0);
            localToWorldMatrix.c2 = new float4(forward, 0);
            localToWorldMatrix.c3 = new float4(position, 1);
            worldToCameraMatrix = MatrixUtility.GetWorldToLocal(localToWorldMatrix);
            worldToCameraMatrix.c0.z = -worldToCameraMatrix.c0.z;
            worldToCameraMatrix.c1.z = -worldToCameraMatrix.c1.z;
            worldToCameraMatrix.c2.z = -worldToCameraMatrix.c2.z;
            worldToCameraMatrix.c3.z = -worldToCameraMatrix.c3.z;
        }
        public void UpdateProjectionMatrix()
        {
            projectionMatrix = Matrix4x4.Ortho(-size, size, -size, size, nearClipPlane, farClipPlane);
        }
    }

    public struct StaticFit
    {
        public int resolution;
        public NativeArray<float3> frustumCorners;
        public Camera mainCamTrans;
    }
    public struct RenderTargets
    {
        public RenderTargetIdentifier renderTargetIdentifier;
        public RenderTargetIdentifier backupIdentifier;
        public int[] gbufferIndex;
        public RenderTargetIdentifier[] gbufferIdentifier;
        public bool initialized;
        public static RenderTargets Init()
        {
            RenderTargets rt;
            rt.gbufferIndex = new int[]
            {
                Shader.PropertyToID("_CameraGBufferTexture0"),
                Shader.PropertyToID("_CameraGBufferTexture1"),
                Shader.PropertyToID("_CameraGBufferTexture2"),
                Shader.PropertyToID("_CameraGBufferTexture3")
            };
            rt.gbufferIdentifier = new RenderTargetIdentifier[4];
            for (int i = 0; i < 4; ++i)
            {
                rt.gbufferIdentifier[i] = rt.gbufferIndex[i];
            }
            rt.backupIdentifier = default;
            rt.renderTargetIdentifier = default;
            rt.initialized = true;
            return rt;
        }
        public RenderTargetIdentifier normalIdentifier
        {
            get { return gbufferIndex[2]; }
        }
    }

    public struct PipelineCommandData
    {
    //    public Matrix4x4 vp;
     //   public Matrix4x4 inverseVP;
        public CommandBuffer buffer;
        public ScriptableRenderContext context;
        public PipelineResources resources;
    }
}