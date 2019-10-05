using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Collections;
using static Unity.Mathematics.math;
using System.IO;
namespace MPipeline
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public unsafe sealed class AnimationDraw : CustomDrawRequest
    {
        public string[] animationPaths;
        public Animator animator;
        private SkinnedMeshRenderer skinRenderer;
        private ComputeBuffer verticesBuffer;
        private ComputeBuffer lastVerticesBuffer;
        private ComputeBuffer skinVerticesBuffer;
        private ComputeBuffer bonesBuffer;
        private List<ComputeBuffer> triangleBuffers;
        private Material[] allMats;
        private NativeArray<float3x4> skinResults;
        private JobHandle handle;
        private ComputeShader skinShader;
        private List<AnimationClip> lstRecord;
        public float animationTime = 0;
        public int clipIndex = 0;
        public bool play = false;
        public bool autoReplay;
        
        private Transform[] bones;
        private Matrix4x4[] bindArr;
        struct Vertex
        {
            public float4 tangent;
            public float3 normal;
            public float3 position;
            public float2 uv;
        }
        struct SkinVertex
        {
            public float4 tangent;
            public float3 normal;
            public float3 position;
            public float2 uv;
            public int4 boneIndex;
            public float4 boneWeight;
        }
        struct AnimationClip
        {
            public AnimationHead head;
            public NativeArray<float3x4> arr;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boundingBoxPosition, boundingBoxExtents * 2);
        }
        private void Awake()
        {
            skinRenderer = GetComponent<SkinnedMeshRenderer>();

            triangleBuffers = new List<ComputeBuffer>(skinRenderer.sharedMesh.subMeshCount);
            for (int i = 0; i < triangleBuffers.Capacity; ++i)
            {
                int[] tris = skinRenderer.sharedMesh.GetTriangles(i);
                var triBuffer = new ComputeBuffer(tris.Length, sizeof(int));
                triBuffer.SetData(tris);
                triangleBuffers.Add(triBuffer);
            }


            Vector3[] verts = skinRenderer.sharedMesh.vertices;
            Vector4[] tans = skinRenderer.sharedMesh.tangents;
            Vector3[] norms = skinRenderer.sharedMesh.normals;
            Vector2[] uvs = skinRenderer.sharedMesh.uv;
            NativeArray<SkinVertex> allVertices = new NativeArray<SkinVertex>(verts.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            BoneWeight[] weights = skinRenderer.sharedMesh.boneWeights;
            SkinVertex* vertsPtr = allVertices.Ptr();
            bindArr = skinRenderer.sharedMesh.bindposes;
            bones = skinRenderer.bones;
            skinResults = new NativeArray<float3x4>(bindArr.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < allVertices.Length; ++i)
            {
                ref var sk = ref vertsPtr[i];
                sk.tangent = tans[i];
                sk.position = verts[i];
                sk.normal = norms[i];
                sk.uv = uvs[i];
                BoneWeight bw = weights[i];
                sk.boneIndex = int4(bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3);
                sk.boneWeight = float4(bw.weight0, bw.weight1, bw.weight2, bw.weight3);
            }
            verticesBuffer = new ComputeBuffer(allVertices.Length, sizeof(Vertex));
            lastVerticesBuffer = new ComputeBuffer(allVertices.Length, sizeof(Vertex));
            skinVerticesBuffer = new ComputeBuffer(allVertices.Length, sizeof(SkinVertex));
            bonesBuffer = new ComputeBuffer(bindArr.Length, sizeof(float3x4));
            skinVerticesBuffer.SetData(allVertices);
            allVertices.Dispose();
            allMats = skinRenderer.sharedMaterials;
            lstRecord = ReadFiles();
        }
        private List<AnimationClip> ReadFiles()
        {
            List<AnimationClip> resultArr = new List<AnimationClip>(animationPaths.Length);
            FileStream[] strs = new FileStream[animationPaths.Length];
            for (int i = 0; i < strs.Length; ++i)
            {
                strs[i] = new FileStream(animationPaths[i], FileMode.Open, FileAccess.Read);
            }
            long maxLen = -1;
            foreach (var i in strs)
            {
                maxLen = max(maxLen, i.Length);
            }
            Debug.Log(maxLen);
            byte[] byteArray = new byte[maxLen];
            foreach (var i in strs)
            {
                i.Read(byteArray, 0, (int)i.Length);
                AnimationHead* head = (AnimationHead*)byteArray.Ptr();
                AnimationClip clip = new AnimationClip
                {
                    head = *head
                };
                NativeArray<float3x4> currArr = new NativeArray<float3x4>((int)((i.Length - sizeof(AnimationHead)) / sizeof(float3x4)), Allocator.Persistent);
                UnsafeUtility.MemCpy(currArr.GetUnsafePtr(), head + 1, currArr.Length * sizeof(float3x4));
                clip.arr = currArr;
                resultArr.Add(clip);
            }
            return resultArr;
        }
        protected override void OnEnableFunc()
        {
            skinRenderer.enabled = false;
        }
        private void OnDestroy()
        {
            verticesBuffer.Dispose();
            lastVerticesBuffer.Dispose();
            skinVerticesBuffer.Dispose();
            skinResults.Dispose();
            bonesBuffer.Dispose();
            foreach (var i in triangleBuffers)
            {
                i.Dispose();
            }
            triangleBuffers.Clear();
            if (lstRecord != null)
            {
                foreach (var i in lstRecord)
                {
                    if (i.arr.IsCreated)
                        i.arr.Dispose();
                }
                lstRecord = null;
            }
        }
        private void DrawPass(CommandBuffer buffer, int pass)
        {
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, verticesBuffer);
            int len = min(triangleBuffers.Count, allMats.Length);
            for (int i = 0; i < len; ++i)
            {
                buffer.SetGlobalBuffer(ShaderIDs.triangleBuffer, triangleBuffers[i]);
                buffer.DrawProcedural(Matrix4x4.identity, allMats[i], pass, MeshTopology.Triangles, triangleBuffers[i].count, 1);
            }
        }
        public override void DrawGBuffer(CommandBuffer buffer)
        {
            DrawPass(buffer, 0);
        }
        public override void DrawShadow(CommandBuffer buffer)
        {
            DrawPass(buffer, 1);
        }
        protected override void DrawCommand(out bool drawGBuffer, out bool drawShadow, out bool drawTransparent)
        {
            drawGBuffer = true;
            drawShadow = true;
            drawTransparent = false;
        }
        BonesTransform jobStruct;
        public override void PrepareJob(PipelineResources resources)
        {
            localToWorldMatrix = transform.localToWorldMatrix;
            skinShader = resources.shaders.gpuSkin;
            if (!play) return;
            AnimationClip clip = lstRecord[clipIndex];
            animationTime += Time.deltaTime;
            animationTime = max(animationTime, 0);
            if (animationTime > clip.head.length)
            {
                if (autoReplay) animationTime -= clip.head.length;
                else animationTime = clip.head.length;
            }
            float currentFrameFloat = (int)(animationTime * clip.head.frameRate);
            int currentFrame = (int)currentFrameFloat;
            if (currentFrame >= clip.arr.Length / clip.head.bonesCount) currentFrame = 0;
            int nextFrame = currentFrame + 1;
            if (nextFrame >= clip.arr.Length / clip.head.bonesCount) nextFrame = 1;
            jobStruct = new BonesTransform
            {
                bindPoses = (float4x4*)bindArr.Ptr(),
                bones = clip.arr.Ptr(),
                bonesCount = clip.head.bonesCount,
                bonesLocal = animator.transform.localToWorldMatrix,
                currentFrame = currentFrame,
                nextFrame = nextFrame,
                lerpValue = frac(currentFrameFloat),
                results = skinResults.Ptr()
            };
            handle = jobStruct.ScheduleRefBurst(clip.head.bonesCount, max(1, clip.head.bonesCount / 4));
        }

        public override void FinishJob()
        {
            if (!play) return;
            ComputeBuffer temp = verticesBuffer;
            verticesBuffer = lastVerticesBuffer;
            lastVerticesBuffer = temp;
            CommandBuffer bf = RenderPipeline.BeforeFrameBuffer;
            bf.SetComputeBufferParam(skinShader, 0, ShaderIDs._SkinVerticesBuffer, skinVerticesBuffer);
            bf.SetComputeBufferParam(skinShader, 0, ShaderIDs._BonesBuffer, bonesBuffer);
            bf.SetComputeBufferParam(skinShader, 0, ShaderIDs.verticesBuffer, verticesBuffer);
            ComputeShaderUtility.Dispatch(skinShader, bf, 0, skinVerticesBuffer.count);
            handle.Complete();
            bonesBuffer.SetData(skinResults);
        }
        [Unity.Burst.BurstCompile]
        private struct BonesTransform : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public float3x4* results;
            [NativeDisableUnsafePtrRestriction]
            public float4x4* bindPoses;
            [NativeDisableUnsafePtrRestriction]
            public float3x4* bones;
            public float4x4 bonesLocal;
            public int bonesCount;
            public int currentFrame;
            public int nextFrame;
            public float lerpValue;
            public void Execute(int index)
            {
                float3x4 lastBone = bones[index + bonesCount * currentFrame];
                float3x4 nextBone = bones[index + bonesCount * nextFrame];
                lastBone.c0 = lerp(lastBone.c0, nextBone.c0, lerpValue);
                lastBone.c1 = lerp(lastBone.c1, nextBone.c1, lerpValue);
                lastBone.c2 = lerp(lastBone.c2, nextBone.c2, lerpValue);
                lastBone.c3 = lerp(lastBone.c3, nextBone.c3, lerpValue);
                float4x4 bone = float4x4(float4(lastBone.c0, 0), float4(lastBone.c1, 0), float4(lastBone.c2, 0), float4(lastBone.c3, 1));
                bone = mul(bonesLocal, bone);
                bone = mul(bone, bindPoses[index]);
                results[index] = float3x4(bone.c0.xyz, bone.c1.xyz, bone.c2.xyz, bone.c3.xyz);
            }
        }
    }
}