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
namespace MPipeline
{
    public unsafe sealed class Test : MonoBehaviour
    {
        public List<SceneStreaming> streamers;
        public SceneStreaming parentStreamer;
        public List<float> distances;
        public Transform point;
        private int level = 0;
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
        public ClusterMatResources tex;
        [EasyButtons.Button]
        void TestCross()
        {
            SceneController.MoveAllScenes(float3(0, 1, 0));
        }

        bool sb;
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Space))
            {
                if (sb)
                    StartCoroutine(SceneStreaming.Combine(parentStreamer, streamers));
                else
                    StartCoroutine(SceneStreaming.Separate(parentStreamer, streamers));
                sb = !sb;
            }
        }
    }
}
