using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe class HLOD : JobProcessEvent
    {
        public int allLevel;
        public List<SceneStreaming> allGPURPScene;
        private NativeList<int> levelOffset;
        public double3 center;
        public double3 extent;
        public struct LoadCommand
        {
            public enum Operator
            {
                Disable, Enable, Combine, Separate
            }
            public Operator ope;
            public int parent;
            public int leftDownSon;
            public int leftUpSon;
            public int rightDownSon;
            public int rightUpSon;
        }
        private NativeQueue<LoadCommand> allLoadingCommand;
        protected override void OnEnableFunc()
        {
            levelOffset = new NativeList<int>(allLevel, Allocator.Persistent);
            levelOffset[0] = 0;
            allLoadingCommand = new NativeQueue<LoadCommand>(20, Allocator.Persistent);
            for (int i = 1; i < allLevel; ++i)
            {
                int v = (int)(pow(2.0, i - 1));
                levelOffset[i] = levelOffset[i - 1] + v * v;
            }
        }

        private IEnumerator Loader()
        {
            while(enabled)
            {
                LoadCommand cmd;
                if (allLoadingCommand.TryDequeue(out cmd))
                {
                    switch(cmd.ope)
                    {
                        case LoadCommand.Operator.Combine:
                            break;
                        case LoadCommand.Operator.Disable:
                            yield return allGPURPScene[cmd.parent].Delete();
                            break;
                        case LoadCommand.Operator.Enable:
                            yield return allGPURPScene[cmd.parent].Generate();
                            break;
                        case LoadCommand.Operator.Separate:
                            break;
                    }
                }
                else yield return null;
            }
        }

        protected override void OnDisableFunc()
        {
            levelOffset.Dispose();
            allLoadingCommand.Dispose();
        }

        public override void PrepareJob()
        {

        }

        public override void FinishJob()
        {

        }
    }

    public unsafe struct HLODQuadTree
    {
        public struct Data
        {
            public int maximumLevel;
            public NativeList_Float lodDistance;
        }
        private HLODQuadTree* leftDown;
        private HLODQuadTree* leftUp;
        private HLODQuadTree* rightDown;
        private HLODQuadTree* rightUp;
        private int2 localPos;
        private int currentLevel;
        private double3 center;
        private bool separate;
        private bool isRendering;
        private double3 extent;
        public HLODQuadTree(int currentLevel, int2 localPos, ref Data data, double3 extent, double3 center)
        {
            separate = false;
            isRendering = false;
            dist = 0;
            this.currentLevel = currentLevel;
            this.center = center;
            this.extent = extent;
            leftDown = null;
            leftUp = null;
            rightDown = null;
            rightUp = null;
            this.localPos = localPos;
        }
        private double dist;

        private void GenerateChildren(ref Data data)
        {
            HLODQuadTree* ptr = MUnsafeUtility.Malloc<HLODQuadTree>(sizeof(HLODQuadTree) * 4, Allocator.Persistent);
            leftDown = ptr;
            leftUp = ptr + 1;
            rightDown = ptr + 2;
            rightUp = ptr + 3;
            int subLevel = currentLevel + 1;
            double3 subExtent = extent * 0.5;
            *leftDown = new HLODQuadTree(subLevel, localPos * 2, ref data, subExtent, center - double3(subExtent.x, 0, subExtent.z));
            *leftUp = new HLODQuadTree(subLevel, localPos * 2 + int2(0, 1), ref data, subExtent, center + double3(-subExtent.x, 0, subExtent.z));
            *rightDown = new HLODQuadTree(subLevel, localPos * 2 + int2(1, 0), ref data, subExtent, center + double3(subExtent.x, 0, -subExtent.z));
            *rightUp = new HLODQuadTree(subLevel, localPos * 2 + 1, ref data, subExtent, center + double3(subExtent.x, 0, subExtent.z));
        }

        private void DisposeChildren()
        {
            if (leftDown != null)
            {
                leftDown->Dispose();
                leftUp->Dispose();
                rightDown->Dispose();
                rightUp->Dispose();
                UnsafeUtility.Free(leftDown, Allocator.Persistent);
                leftDown = null;
            }
        }

        private void Combine(bool willRender)
        {
            //TODO
        }

        public void FirstUpdate(double3 cameraPos, ref Data data)
        {
            if (leftDown != null)
            {
                leftDown->FirstUpdate(cameraPos, ref data);
                leftUp->FirstUpdate(cameraPos, ref data);
                rightDown->FirstUpdate(cameraPos, ref data);
                rightUp->FirstUpdate(cameraPos, ref data);
            }
            double3 boxToCamera = cameraPos - center;
            dist = MathLib.DistanceToCube(extent, boxToCamera);
            if (dist > data.lodDistance[currentLevel])
            {
                separate = false;
            }
            else if (dist > data.lodDistance[currentLevel + 1])
            {
                separate = false;
            }
            else
                separate = true;
        }

        public void Dispose()
        {
            DisposeChildren();
        }
    }
}