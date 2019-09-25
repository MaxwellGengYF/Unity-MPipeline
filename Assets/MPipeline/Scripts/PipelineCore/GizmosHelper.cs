using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace MPipeline
{
    internal struct GizmosHelper
    {
        Color color;
        Matrix4x4 mat;
        Texture tex;

        public void Init()
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
