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

        private void Update()
        {
            float dist = distance(point.position, transform.position);
            if (dist < distances[level])
            {
                if (level > 0)
                {
                    if (dist < distances[level - 1])
                    {
                        level--;
                        StartCoroutine(streamers[level].Generate());
                        StartCoroutine(streamers[level + 1].Delete());
                    }
                }
                else if(streamers[0].state == SceneStreaming.State.Unloaded)
                {
                    StartCoroutine(streamers[0].Generate());
                }
            }
            else
            {
                if (level < streamers.Count - 1)
                {
                    level++;
                    StartCoroutine(streamers[level].Generate());
                    StartCoroutine(streamers[level - 1].Delete());
                }
                else if(streamers[level].state == SceneStreaming.State.Loaded)
                {
                    StartCoroutine(streamers[level].Delete());
                }
            }

            /*int value;
            if (int.TryParse(Input.inputString, out value))
            {
                if (value < streamers.Count)
                {
                    if (streamers[value].state == SceneStreaming.State.Unloaded)
                    {
                        StartCoroutine(streamers[value].Generate());
                    }
                    else if (streamers[value].state == SceneStreaming.State.Loaded)
                    {
                        StartCoroutine(streamers[value].Delete());
                    }
                }
            }*/
        }
    }
}
