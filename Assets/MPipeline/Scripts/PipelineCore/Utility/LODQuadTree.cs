using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
namespace MPipeline
{
    public interface LODExecutor
    {
        void Enable(double3 position, int objectIndex);
        void Disable();
    }

    public unsafe struct LODQuadTreeData<T> where T : struct, LODExecutor
    {
        public T executor;
    }

    public unsafe struct LODQuadTree
    {
        private LODQuadTree* leftDown;
        private LODQuadTree* leftUp;// => sons + 1;
        private LODQuadTree* rightDown;// => sons + 2;
        private LODQuadTree* rightUp;// => sons + 3;
        private double3 center;
        private double3 extent;
        public LODQuadTree(double3 center, double3 extent)
        {
            leftDown = null;
            leftUp = null;
            rightDown = null;
            rightUp = null;
            this.center = center;
            this.extent = extent;
        }
    }
}
