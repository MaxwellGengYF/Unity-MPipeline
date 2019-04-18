using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Animation Test")]
    public unsafe class AnimationTestEvent : PipelineEvent
    {
        #region CONST
        const int AnimationUpdateKernel = 0;
        const int BoneUpdateKernel = 1;
        const int SkinUpdateKernel = 2;
        #endregion
        #region VARIABLE
        public Material animationMaterial;
        private ComputeBuffer objBuffer;
        private ComputeBuffer bonesBuffer;
        private ComputeBuffer verticesBuffer;
        private ComputeBuffer resultBuffer;
        private ComputeBuffer bindBuffer;
        public Texture2D animTex;
        public Transform[] characterPoints;
        public Mesh targetMesh;
        public int framePerSecond = 30;
        private MaterialPropertyBlock block;
        private int[] _ModelBones = new int[2];
        private int bindPoseCount;
        #endregion
        public override bool CheckProperty()
        {
            return animationMaterial != null;
        }
        protected override void Init(PipelineResources resources)
        {
            block = new MaterialPropertyBlock();
            objBuffer = new ComputeBuffer(characterPoints.Length, sizeof(AnimState));
            Matrix4x4[] bindPosesArray = targetMesh.bindposes;
            bindPoseCount = bindPosesArray.Length;
            bonesBuffer = new ComputeBuffer(bindPoseCount * characterPoints.Length, sizeof(Matrix3x4));
            bindBuffer = new ComputeBuffer(bindPoseCount, sizeof(Matrix3x4));
            NativeArray<Matrix3x4> bindNative = new NativeArray<Matrix3x4>(bindPoseCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            Matrix3x4* bindPtr = bindNative.Ptr();
            for(int i = 0; i < bindPoseCount; ++i)
            {
                *bindPtr = new Matrix3x4(ref bindPosesArray[i]);
                bindPtr++;
            }
            bindBuffer.SetData(bindNative);
            bindNative.Dispose();
            int[] triangles = targetMesh.triangles;
            Vector3[] vertices = targetMesh.vertices;
            Vector3[] normals = targetMesh.normals;
            Vector4[] tangents = targetMesh.tangents;
            BoneWeight[] weights = targetMesh.boneWeights;
            Vector2[] uv = targetMesh.uv;
            NativeArray<SkinPoint> allSkinPoints = new NativeArray<SkinPoint>(triangles.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            SkinPoint* pointsPtr = allSkinPoints.Ptr();
            for (int i = 0; i < triangles.Length; ++i)
            {
                SkinPoint* currentPtr = pointsPtr + i;
                int index = triangles[i];
                currentPtr->position = vertices[index];
                currentPtr->tangent = tangents[index];
                currentPtr->normal = normals[index];
                ref BoneWeight bone = ref weights[index];
                currentPtr->boneWeight = new Vector4(bone.weight0, bone.weight1, bone.weight2, bone.weight3);
                currentPtr->boneIndex = new Vector4Int(bone.boneIndex0, bone.boneIndex1, bone.boneIndex2, bone.boneIndex3);
                currentPtr->uv = uv[index];
            }
            verticesBuffer = new ComputeBuffer(allSkinPoints.Length, sizeof(SkinPoint));
            verticesBuffer.SetData(allSkinPoints);
            resultBuffer = new ComputeBuffer(allSkinPoints.Length * characterPoints.Length, sizeof(Vector3));
            block.SetBuffer(ShaderIDs.resultBuffer, resultBuffer);
            allSkinPoints.Dispose();
            NativeArray<AnimState> allAnimState = new NativeArray<AnimState>(characterPoints.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            AnimState* animStatePtr = allAnimState.Ptr();
            for (int i = 0; i < characterPoints.Length; ++i)
            {
                animStatePtr->localToWorldMatrix = new Matrix3x4(characterPoints[i].localToWorldMatrix);
                animStatePtr->frame = Random.Range(0, animTex.width - 1e-4f);
                animStatePtr++;
            }
            objBuffer.SetData(allAnimState);
            allAnimState.Dispose();
        }

        protected override void Dispose()
        {
            objBuffer.Dispose();
            bonesBuffer.Dispose();
            verticesBuffer.Dispose();
            resultBuffer.Dispose();
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            ComputeShader shader = data.resources.shaders.gpuSkin;
            int* pointer = stackalloc int[] {bindPoseCount , verticesBuffer.count };
            _ModelBones.CopyFrom(pointer, 2);
            shader.SetInts(ShaderIDs._ModelBones, _ModelBones);
            shader.SetVector(ShaderIDs._TimeVar, new Vector4(Time.deltaTime * framePerSecond, animTex.width - 1e-4f));
            shader.SetBuffer(AnimationUpdateKernel, ShaderIDs.objBuffer, objBuffer);
            shader.SetBuffer(BoneUpdateKernel, ShaderIDs.objBuffer, objBuffer);
            shader.SetBuffer(BoneUpdateKernel, ShaderIDs.bonesBuffer, bonesBuffer);
            shader.SetTexture(BoneUpdateKernel, ShaderIDs._AnimTex, animTex);
            shader.SetBuffer(BoneUpdateKernel, ShaderIDs.bindBuffer, bindBuffer);
            shader.SetBuffer(SkinUpdateKernel, ShaderIDs.bonesBuffer, bonesBuffer);
            shader.SetBuffer(SkinUpdateKernel, ShaderIDs.verticesBuffer, verticesBuffer);
            shader.SetBuffer(SkinUpdateKernel, ShaderIDs.resultBuffer, resultBuffer);
            shader.SetBuffer(SkinUpdateKernel, ShaderIDs.objBuffer, objBuffer); //Debug
            const int THREAD = 256;
            ComputeShaderUtility.Dispatch(shader, buffer, AnimationUpdateKernel, characterPoints.Length, THREAD);
            ComputeShaderUtility.Dispatch(shader, buffer, BoneUpdateKernel, bonesBuffer.count, THREAD);
            ComputeShaderUtility.Dispatch(shader, buffer, SkinUpdateKernel, resultBuffer.count, THREAD);
            buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthBuffer);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.DrawProcedural(Matrix4x4.identity, animationMaterial, 0, MeshTopology.Triangles, resultBuffer.count, characterPoints.Length, block);
            data.ExecuteCommandBuffer();
        }
    }
    /*
    public unsafe struct GetBoneJob : IJobParallelForTransform
    {
        [NativeDisableUnsafePtrRestriction]
        public Matrix3x4* matrices;
        [NativeDisableUnsafePtrRestriction]
        public Matrix4x4* bindPoses;
        public void Execute(int index, TransformAccess transform)
        {
            matrices[index] = new Matrix3x4(Matrix4x4.TRS(transform.position, transform.rotation,transform.localScale) * bindPoses[index]);
        }
    }*/
    public struct AnimState
    {
        public Matrix3x4 localToWorldMatrix;
        public float frame;
    };
    public struct SkinPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector2 uv;
        public Vector4 boneWeight;
        public Vector4Int boneIndex;
    };
}