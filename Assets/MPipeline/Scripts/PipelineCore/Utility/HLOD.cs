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
        public int[] allLodDistances;
        public Transform cam;
        public List<SceneStreaming> allGPURPScene;
        private NativeList_Int levelOffset;
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
        private HLODQuadTree tree;
        private QuadTreeJob quadTreeJob;
        private JobHandle quadTreeHandle;
        private List<SceneStreaming> childrenList = new List<SceneStreaming>(4);
        protected override void OnEnableFunc()
        {
            int allLevel = allLodDistances.Length;
            levelOffset = new NativeList_Int(allLevel, allLevel, Allocator.Persistent);
            levelOffset[0] = 0;
            for (int i = 1; i < allLevel; ++i)
            {
                int v = (int)(pow(2.0, i - 1));
                levelOffset[i] = levelOffset[i - 1] + v * v;
            }
            quadTreeJob.data = new HLODQuadTree.Data
            {
                commands = new NativeQueue<LoadCommand>(20, Allocator.Persistent),
                lodDistance = new NativeList_Float(allLevel + 1, allLevel + 1, Allocator.Persistent),
                positionOffsets = levelOffset
            };
            for (int i = 0; i < allLevel; ++i)
            {
                quadTreeJob.data.lodDistance[i] = allLodDistances[i];
            }
            quadTreeJob.data.lodDistance[allLevel] = 0;
            tree = new HLODQuadTree(0, 0, ref quadTreeJob.data, extent, (float3)transform.position);
            quadTreeJob.treePtr = tree.Ptr();
            StartCoroutine(Loader());
        }

        private IEnumerator Loader()
        {
            while (enabled)
            {
                LoadCommand cmd;
                if (quadTreeJob.data.commands.TryDequeue(out cmd))
                {
                    switch (cmd.ope)
                    {
                        case LoadCommand.Operator.Combine:
                            childrenList.Clear();
                            if (allGPURPScene[cmd.leftDownSon]) childrenList.Add(allGPURPScene[cmd.leftDownSon]);
                            if (allGPURPScene[cmd.leftUpSon]) childrenList.Add(allGPURPScene[cmd.leftUpSon]);
                            if (allGPURPScene[cmd.rightDownSon]) childrenList.Add(allGPURPScene[cmd.rightDownSon]);
                            if (allGPURPScene[cmd.rightUpSon]) childrenList.Add(allGPURPScene[cmd.rightUpSon]);
                            yield return SceneStreaming.Combine(allGPURPScene[cmd.parent], childrenList);
                            break;
                        case LoadCommand.Operator.Disable:
                            yield return allGPURPScene[cmd.parent].Delete();
                            break;
                        case LoadCommand.Operator.Enable:
                            yield return allGPURPScene[cmd.parent].Generate();
                            break;
                        case LoadCommand.Operator.Separate:
                            childrenList.Clear();
                            if (allGPURPScene[cmd.leftDownSon]) childrenList.Add(allGPURPScene[cmd.leftDownSon]);
                            if (allGPURPScene[cmd.leftUpSon]) childrenList.Add(allGPURPScene[cmd.leftUpSon]);
                            if (allGPURPScene[cmd.rightDownSon]) childrenList.Add(allGPURPScene[cmd.rightDownSon]);
                            if (allGPURPScene[cmd.rightUpSon]) childrenList.Add(allGPURPScene[cmd.rightUpSon]);
                            yield return SceneStreaming.Separate(allGPURPScene[cmd.parent], childrenList);
                            break;
                    }
                }
                yield return null;
            }
        }

        protected override void OnDisableFunc()
        {
            levelOffset.Dispose();
            quadTreeJob.data.commands.Dispose();
            quadTreeJob.data.lodDistance.Dispose();
        }

        public override void PrepareJob()
        {
            quadTreeJob.cameraPos = (float3)cam.position;
            quadTreeHandle = quadTreeJob.Schedule();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireCube(transform.position, (float3)(extent * 2));
        }

        public override void FinishJob()
        {
            quadTreeHandle.Complete();
        }

        private struct QuadTreeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public HLODQuadTree* treePtr;
            public double3 cameraPos;
            public HLODQuadTree.Data data;
            public void Execute()
            {
                treePtr->FirstUpdate(cameraPos, ref data);
                treePtr->SecondUpdate(ref data);
            }
        }
    }

    public unsafe struct HLODQuadTree
    {
        public struct Data
        {
            public NativeList_Float lodDistance;
            public NativeList_Int positionOffsets;
            public NativeQueue<HLOD.LoadCommand> commands;
        }
        private HLODQuadTree* leftDown;
        private HLODQuadTree* leftUp;
        private HLODQuadTree* rightDown;
        private HLODQuadTree* rightUp;
        private int2 localPos;
        private int currentLevel;
        private double3 center;
        private bool separate;
        private bool m_isRendering;
        private static int GetIndex(int2 position, int level, ref Data data)
        {
            return data.positionOffsets[level] + position.x + position.y * (int)(0.1 + pow(2.0, level));
        }
        public void SetIsRendering(bool value, ref Data data)
        {
            if (m_isRendering == value) return;
            m_isRendering = value;
            if (value)
            {
                data.commands.Add(new HLOD.LoadCommand
                {
                    ope = HLOD.LoadCommand.Operator.Enable,
                    parent = GetIndex(localPos, currentLevel, ref data)
                });
            }
            else
            {
                data.commands.Add(new HLOD.LoadCommand
                {
                    ope = HLOD.LoadCommand.Operator.Disable,
                    parent = GetIndex(localPos, currentLevel, ref data)
                });
            }
        }
        private double3 extent;
        public HLODQuadTree(int currentLevel, int2 localPos, ref Data data, double3 extent, double3 center)
        {
            Debug.Log(currentLevel);
            separate = false;
            m_isRendering = false;
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
            *leftUp = new HLODQuadTree(subLevel, localPos * 2 + int2(1, 0), ref data, subExtent, center + double3(-subExtent.x, 0, subExtent.z));
            *rightDown = new HLODQuadTree(subLevel, localPos * 2 + int2(0, 1), ref data, subExtent, center + double3(subExtent.x, 0, -subExtent.z));
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
                leftUp = null;
                rightDown = null;
                rightUp = null;

            }
        }

        private void Combine(bool willRender, ref Data data)
        {
            if (leftDown != null)
            {
                leftDown->m_isRendering = false;
                leftUp->m_isRendering = false;
                rightDown->m_isRendering = false;
                rightUp->m_isRendering = false;
                m_isRendering = true;
                data.commands.Add(new HLOD.LoadCommand
                {
                    leftDownSon = GetIndex(leftDown->localPos, leftDown->currentLevel, ref data),
                    leftUpSon = GetIndex(leftUp->localPos, leftUp->currentLevel, ref data),
                    rightDownSon = GetIndex(rightDown->localPos, rightDown->currentLevel, ref data),
                    rightUpSon = GetIndex(rightUp->localPos, rightUp->currentLevel, ref data),
                    ope = HLOD.LoadCommand.Operator.Combine,
                    parent = GetIndex(localPos, currentLevel, ref data)
                });
                DisposeChildren();
            }
            else
            {
                SetIsRendering(willRender, ref data);
            }
        }

        private void Separate(ref Data data)
        {
            if (currentLevel >= data.lodDistance.Length - 2)
            {
                SetIsRendering(true, ref data);
            }
            else if (leftDown == null)
            {
                GenerateChildren(ref data);
                leftDown->m_isRendering = true;
                leftUp->m_isRendering = true;
                rightDown->m_isRendering = true;
                rightUp->m_isRendering = true;
                m_isRendering = false;
                data.commands.Add(new HLOD.LoadCommand
                {
                    leftDownSon = GetIndex(leftDown->localPos, leftDown->currentLevel, ref data),
                    leftUpSon = GetIndex(leftUp->localPos, leftUp->currentLevel, ref data),
                    rightDownSon = GetIndex(rightDown->localPos, rightDown->currentLevel, ref data),
                    rightUpSon = GetIndex(rightUp->localPos, rightUp->currentLevel, ref data),
                    ope = HLOD.LoadCommand.Operator.Separate,
                    parent = GetIndex(localPos, currentLevel, ref data)
                });
            }
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
                Combine(currentLevel > 0, ref data);
            }
            else if (dist > data.lodDistance[currentLevel + 1])
            {
                separate = false;
                Combine(true, ref data);
            }
            else
                separate = true;
        }

        public void SecondUpdate(ref Data data)
        {
            if (separate)
            {
                Separate(ref data);
                if (leftDown != null)
                {
                    leftDown->SecondUpdate(ref data);
                    leftUp->SecondUpdate(ref data);
                    rightDown->SecondUpdate(ref data);
                    rightUp->SecondUpdate(ref data);
                }
            }
        }

        public void Dispose()
        {
            DisposeChildren();
        }
    }
}