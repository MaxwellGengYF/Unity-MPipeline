using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public struct AreaLight
    {
        public float4x4 mat;
        public float3 color;
    };
    public struct TubeLight
    {
        public float3 start;
        public float range;
        public float3 end;
        public float radius;
        public float3 color;
    };
    public unsafe sealed class AreaVolumeProbe : MonoBehaviour
    {
        public struct AreaLightComponent
        {
            public AreaLight area;
            public float4x4 localToWorld;
            public float3 center;
            public float3 extent;
            public void* componentPtr;
        }

        public static NativeList<AreaLightComponent> allAreaLight { get; private set; }
        [Range(1f, 170f)]
        public float angle = 60f;
        private int index;
        public Color color = Color.white;
        public float intensity = 1;
        private void OnEnable()
        {
            if (!allAreaLight.isCreated) allAreaLight = new NativeList<AreaLightComponent>(20, Allocator.Persistent);
            index = allAreaLight.Length;
            Bounds bound = GetFrustumBounds();
            allAreaLight.Add(new AreaLightComponent
            {
                area = new AreaLight
                {
                    mat = GetProjectionMatrix(true),
                    color = float3(color.r, color.g, color.b) * intensity
                },
                localToWorld = transform.localToWorldMatrix,
                center = bound.center,
                extent = bound.extents,
                componentPtr = MUnsafeUtility.GetManagedPtr(this)
            });
        }
        [EasyButtons.Button]
        public void UpdateLight()
        {
            Bounds bound = GetFrustumBounds();
            allAreaLight[index] = new AreaLightComponent
            {
                area = new AreaLight
                {
                    mat = GetProjectionMatrix(true),
                    color = float3(color.r, color.g, color.b) * intensity
                },
                localToWorld = transform.localToWorldMatrix,
                center = bound.center,
                extent = bound.extents,
                componentPtr = MUnsafeUtility.GetManagedPtr(this)
            };
        }

        private void OnDisable()
        {
            AreaVolumeProbe lastProbe = MUnsafeUtility.GetObject<AreaVolumeProbe>(allAreaLight[allAreaLight.Length - 1].componentPtr);
            lastProbe.index = index;
            allAreaLight[index] = allAreaLight[allAreaLight.Length - 1];
            allAreaLight.RemoveLast();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;

            float near = GetNearToCenter();
            Gizmos.matrix = transform.localToWorldMatrix * GetOffsetMatrix(-near);

            Gizmos.DrawFrustum(Vector3.zero, angle, near + 1, near, 1);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Bounds bounds = GetFrustumBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        float GetNearToCenter()
        {
            return 0.5f / Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
        }

        Matrix4x4 GetOffsetMatrix(float zOffset)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.SetColumn(3, new Vector4(0, 0, zOffset, 1));
            return m;
        }

        Matrix4x4 PerspectiveLinearZ(float fov, float aspect, float near, float far)
        {
            // A vector transformed with this matrix should get perspective division on x and y only:
            // Vector4 vClip = MultiplyPoint(PerspectiveLinearZ(...), vEye);
            // Vector3 vNDC = Vector3(vClip.x / vClip.w, vClip.y / vClip.w, vClip.z);
            // vNDC is [-1, 1]^3 and z is linear, i.e. z = 0 is half way between near and far in world space.

            float rad = Mathf.Deg2Rad * fov * 0.5f;
            float cotan = Mathf.Cos(rad) / Mathf.Sin(rad);
            float deltainv = 1.0f / (far - near);
            Matrix4x4 m;

            m.m00 = cotan / aspect; m.m01 = 0.0f; m.m02 = 0.0f; m.m03 = 0.0f;
            m.m10 = 0.0f; m.m11 = cotan; m.m12 = 0.0f; m.m13 = 0.0f;
            m.m20 = 0.0f; m.m21 = 0.0f; m.m22 = 2.0f * deltainv; m.m23 = -(far + near) * deltainv;
            m.m30 = 0.0f; m.m31 = 0.0f; m.m32 = 1.0f; m.m33 = 0.0f;

            return m;
        }

        public Matrix4x4 GetProjectionMatrix(bool linearZ = false)
        {
            Matrix4x4 m;

            if (angle == 0.0f)
            {
                m = Matrix4x4.Ortho(-0.5f, 0.5f , -0.5f , 0.5f , 0, -1);
            }
            else
            {
                float near = GetNearToCenter();
                if (linearZ)
                {
                    m = PerspectiveLinearZ(angle, 1, near, near + 1);
                }
                else
                {
                    m = Matrix4x4.Perspective(angle, 1, near, near + 1);
                    m = m * Matrix4x4.Scale(new Vector3(1, 1, -1));
                }
                m = m * GetOffsetMatrix(near);
            }
            m = m * transform.worldToLocalMatrix;
            return m;
        }


        private Bounds GetFrustumBounds()
        {
            float3 m_Size = transform.localScale;
            float tanhalffov = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
            float near = m_Size.y * 0.5f / tanhalffov;
            float z = m_Size.z;
            float y = (near + m_Size.z) * tanhalffov * 2.0f;
            float x = m_Size.x * y / m_Size.y;
            return new Bounds(Vector3.forward * m_Size.z * 0.5f, new Vector3(x, y, z));
        }
    }
}