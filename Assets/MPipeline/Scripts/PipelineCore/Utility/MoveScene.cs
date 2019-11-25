using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using Unity.Collections;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe sealed class MoveScene : JobProcessEvent
    {
        private NativeDictionary<ulong, int, PtrEqual> objToTransform;
        private TransformAccessArray tranArray;
        private List<Transform> allTransforms;
        public const int INIT_CAPA = 50;
        public static MoveScene current { get; private set; }
        protected override void OnEnableFunc()
        {
            objToTransform = new NativeDictionary<ulong, int, PtrEqual>(INIT_CAPA, Allocator.Persistent, new PtrEqual());
            tranArray = new TransformAccessArray(INIT_CAPA);
            allTransforms = new List<Transform>(INIT_CAPA);
            current = this;
        }

        protected override void OnDisableFunc()
        {
            current = null;
            objToTransform.Dispose();
            tranArray.Dispose();
            allTransforms = null;
        }

        public void AddTransform(Transform tran)
        {
            ulong ptrAddress = (ulong)MUnsafeUtility.GetManagedPtr(tran);
            int index = allTransforms.Count;
            allTransforms.Add(tran);
            objToTransform.Add(ptrAddress, index);
            tranArray.Add(tran);
        }

        public void RemoveTransform(Transform tran)
        {
            ulong ptrAddress = (ulong)MUnsafeUtility.GetManagedPtr(tran);
            int targetIndex;
            if (!objToTransform.Get(ptrAddress, out targetIndex)) return;
            objToTransform.Remove(ptrAddress);
            if (targetIndex != (allTransforms.Count - 1))
            {
                allTransforms[targetIndex] = allTransforms[allTransforms.Count - 1];
                ulong lastPtrAddress = (ulong)MUnsafeUtility.GetManagedPtr(allTransforms[targetIndex]);
                objToTransform[lastPtrAddress] = targetIndex;
            }
            tranArray.RemoveAtSwapBack(targetIndex);
            allTransforms.RemoveAt(allTransforms.Count - 1);
        }
        private JobHandle moveHandle;
        private bool move = false;
        private float3 moveDist = 0;
        public void Move(float3 targetDist)
        {
            move = true;
            moveDist += targetDist;
            RenderPipeline.MoveSceneCamera(targetDist);
        }
        public override void PrepareJob()
        {
            if (move)
            {
                moveHandle = new MoveJob
                {
                    deltaDir = moveDist
                }.Schedule(tranArray);
            }
           
        }

        public override void FinishJob()
        {
            if (move)
            {
                if(MTerrain.current)
                {
                    MTerrain.current.MoveTerrain(moveDist);
                }
                SceneController.MoveAllScenes(moveDist);
                move = false;
                moveDist = 0;
                moveHandle.Complete();
            }

        }
        [Unity.Burst.BurstCompile]
        public struct MoveJob : IJobParallelForTransform
        {
            public float3 deltaDir;
            public void Execute(int index, TransformAccess access)
            {
                access.position = (float3)access.position + deltaDir;
            }
        }
    }
}
