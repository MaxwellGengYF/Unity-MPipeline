using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public abstract class TextureDecalBase : MonoBehaviour
    {
        [Range(0f, 1f)]
        public float blendWeight = 0.5f;
        public bool useVoronoiSample = false;
        private bool initialized = false;
        public void RunInit()
        {
            if (initialized) return;
            initialized = true;
            Init();
        }


        public void RunDispose()
        {
            if (!initialized) return;
            initialized = false;
            Dispose();
        }
        public void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        protected abstract void Init();
        public abstract Texture GetDecal(CommandBuffer buffer);
        protected abstract void Dispose();
    }
}
