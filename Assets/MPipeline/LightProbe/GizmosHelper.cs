using System;
using UnityEngine;


namespace MPipeline
{
    internal class GizmosHelper : IDisposable
    {
        Color color;
        Matrix4x4 mat;
        Texture tex;

        private bool disposed = false;

        public GizmosHelper()
        {
            color = Gizmos.color;
            mat = Gizmos.matrix;
            tex = Gizmos.exposure;
        }

        public void Dispose()
        {
             Gizmos.color = color;
             Gizmos.matrix = mat;
             Gizmos.exposure = tex;
        }

    }

}
