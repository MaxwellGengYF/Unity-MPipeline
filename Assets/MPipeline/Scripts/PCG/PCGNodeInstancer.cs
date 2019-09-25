#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Unity.Mathematics;
using Unity.Collections;
using static Unity.Mathematics.math;
namespace MPipeline.PCG
{
    [ExecuteInEditMode]
    public class PCGNodeInstancer : MonoBehaviour
    {
        public int3 instanceCount = 1;
        public GameObject instancePrefab;
        private List<GameObject> instancedObjects = new List<GameObject>();
        private void OnEnable()
        {
            if(!instancePrefab)
            {
                enabled = false;
                return;
            }
            instancePrefab.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        }
        private void ClearInstanceObject()
        {
            foreach(var i in instancedObjects)
            {
                DestroyImmediate(i);
            }
            instancedObjects.Clear();
        }

        private void OnDisable()
        {
            ClearInstanceObject();
        }

        [EasyButtons.Button]
        public void UpdateInstance()
        {
            if (!enabled) return;
            ClearInstanceObject();
            for(int x = 0; x < instanceCount.x; ++x)
                for(int y = 0; y < instanceCount.y; ++y)
                    for(int z = 0; z < instanceCount.z; ++z)
                    {
                        float3 uv = (float3(x, y, z) + 0.5f) / instanceCount;
                        float3 localPos = lerp(-0.5f, 0.5f, uv);
                        float3 worldPos = mul(transform.localToWorldMatrix, float4(localPos, 1)).xyz;
                        GameObject newObj = Instantiate(instancePrefab, worldPos, transform.rotation, null) as GameObject;
                        newObj.SetActive(true);
                        instancedObjects.Add(newObj);
                    }
        }
    }
}
#endif