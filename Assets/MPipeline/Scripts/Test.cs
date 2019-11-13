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
            Debug.Log(System.Guid.NewGuid().ToString());
        }

        private void Update()
        {
            
            int value;
            if (int.TryParse(Input.inputString, out value))
            {
                if(value < streamers.Count)
                {
                    if(streamers[value].state == SceneStreaming.State.Unloaded)
                    {
                        StartCoroutine(streamers[value].Generate());
                    }
                    else if(streamers[value].state == SceneStreaming.State.Loaded)
                    {
                        StartCoroutine(streamers[value].Delete());
                    }
                }
            }
            
        }

    }
}
