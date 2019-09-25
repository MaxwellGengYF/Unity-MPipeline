#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = Unity.Mathematics.Random;
namespace MPipeline.PCG
{
    public class PCGEmptyNode : PCGNodeBase
    {
        public enum UpdateRandomType
        {
            Unchange, Random, Parent
        }
        public UpdateRandomType randomType = UpdateRandomType.Parent;
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        public override Bounds GetBounds()
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        public override NativeList<Point> GetPointsResult(out Material[] targetMaterials)
        {
            targetMaterials = new Material[0];
            return new NativeList<Point>(1, Unity.Collections.Allocator.Temp);
        }

        public override void UpdateSettings()
        {
            var nodes = GetComponentsInChildren<PCGNodeBase>();
            switch (randomType)
            {
                case UpdateRandomType.Parent:
                    foreach (var i in nodes)
                    {
                        if (i == this) continue;
                        if (i.enabled)
                        {
                            i.RandomSeed = RandomSeed;
                            i.UpdateSettings();
                        }
                    }
                    break;
                case UpdateRandomType.Unchange:
                    foreach (var i in nodes)
                    {
                        if (i == this) continue;
                        if (i.enabled)
                        {
                            i.UpdateSettings();
                        }
                    }
                    break;
                default:
                    Random rd = new Random(RandomSeed);
                    foreach (var i in nodes)
                    {
                        if (i == this) continue;
                        if (i.enabled)
                        {
                            i.RandomSeed = rd.NextUInt();
                            i.UpdateSettings();
                        }
                    }
                    break;
            }
        }

        public override void Init(PCGResources resources)
        {

        }

        public override void Dispose()
        {

        }
    }
}
#endif