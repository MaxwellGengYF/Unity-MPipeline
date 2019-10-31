#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using MPipeline;
using Unity.Jobs;
using UnityEngine.Rendering;
using Unity.Collections;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Random = UnityEngine.Random;
using UnityEngine.AddressableAssets;
using MPipeline.PCG;
namespace MPipeline
{
    public unsafe sealed class Test : MonoBehaviour
    {
        [EasyButtons.Button]
        void EnableWhiteModel()
        {
            Shader.EnableKeyword("USE_WHITE");
        }

        [EasyButtons.Button]
        void DisableWhiteModel()
        {
            Shader.DisableKeyword("USE_WHITE");
        }

        [EasyButtons.Button]
        void TestCross()
        {
            Debug.Log(cross(float3(0, 0, 1), float3(1, 0, 0)));
        }

        private void Update()
        {
            /*
            int value;
            if (int.TryParse(Input.inputString, out value))
            {
                var clusterResources = RenderPipeline.current.resources.clusterResources;
                clusterResources.TransformScene((uint)value, this);
            }
            */
        }

    }
}
#endif