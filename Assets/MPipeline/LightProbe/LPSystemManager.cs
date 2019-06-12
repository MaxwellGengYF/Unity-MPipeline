using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MPipeline
{
    internal class LPSystemManager : MonoBehaviour
    {
        [SerializeField]
        public bool showDirtArea = true;

        [SerializeField]
        public LPSceneFile sceneLightProbeFile;

        [System.Serializable]
        public class Chunk_pg {
            [SerializeField]
            public List<LightProbeVolmue> pgs;
            public Chunk_pg() {
                pgs = new List<LightProbeVolmue>();
            }
        }

        [SerializeField, HideInInspector]
        List<Vector2Int> dirtChunkId;
        [SerializeField, HideInInspector]
        List<Chunk_pg> dirtChunks;


#if UNITY_EDITOR

        public void MarkDirt(Vector2Int id, LightProbeVolmue group)
        {
            if (dirtChunks == null)
            {
                dirtChunkId = new List<Vector2Int>();
                dirtChunks = new List<Chunk_pg>();
            }
            Chunk_pg r = null;
            for (int i = 0; i < dirtChunkId.Count; i++)
            {
                if (dirtChunkId[i] == id)
                {
                    r = dirtChunks[i]; break;
                }
            }
            if (r == null)
            {
                r = new Chunk_pg();
                dirtChunkId.Add(id);
                dirtChunks.Add(r);
                EditorUtility.SetDirty(this);
            }
            if (r.pgs.Contains(group)) return;
            r.pgs.Add(group);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public void RebakeDirtChunks()
        {
            #region init

            var resources = AssetDatabase.LoadAssetAtPath<LPResources>("Assets/MPipeline/LightProbe/Resources/LPResources.asset");

            ComputeShader cs_GetSurfelFromGBuffer = resources.GetSurfelFromGBuffer;

            GameObject go = new GameObject();
            Camera cam = go.AddComponent<Camera>();
            var info = go.AddComponent<BakeLightProbeInfomation>();
            cam.cameraType = (CameraType)32;
            go.SetActive(false);
            cam.enabled = false;
            cam.aspect = 1;
            cam.transform.up = Vector3.up;
            cam.transform.forward = Vector3.forward;
            float distance = 30;
            cam.orthographicSize = distance;
            cam.farClipPlane = distance * 2;

            RenderTextureDescriptor rtd = new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGBFloat, 24);
            rtd.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            rtd.autoGenerateMips = false;
            rtd.useMipMap = false;
            info.rt2 = new RenderTexture(rtd);
            info.rt2.Create();
            info.rt2.filterMode = FilterMode.Point;
            info.rt3 = new RenderTexture(rtd);
            info.rt3.Create();
            info.rt3.filterMode = FilterMode.Point;

            rtd = new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGBFloat, 0);
            rtd.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            info.rt0 = new RenderTexture(rtd);
            info.rt0.Create();
            info.rt0.filterMode = FilterMode.Point;
            info.rt1 = new RenderTexture(rtd);
            info.rt1.Create();
            info.rt1.filterMode = FilterMode.Point;
            #endregion

            for (int i = 0; i < dirtChunkId.Count; i++)
            {
                Vector2Int id = dirtChunkId[i];

                List<LPProbe> probes = new List<LPProbe>();

                Bounds bound = new Bounds(new Vector3(id.x * 64 + 32, 0, id.y * 64 + 32), new Vector3(64, 999999999, 64));

                foreach (var group in dirtChunks[i].pgs)
                {
                    foreach (var probe in group.probePositions)
                    {
                        if (bound.Contains(probe))
                        {
                            probes.Add(new LPProbe(probe));
                        }
                    }
                }

                LPChunk chunk = sceneLightProbeFile.GetChunkFile(id);
                chunk.probes = probes.ToArray();
                EditorUtility.SetDirty(chunk);

                Dictionary<Vector3Int, List<LPSurfel>> surfels = new Dictionary<Vector3Int, List<LPSurfel>>();

                for (int j = 0; j < probes.Count; j++)
                {
                    int probe_id = j;

                    cam.transform.position = probes[j].position;

                    cam.Render();

                    // now get cubemap GBuffer, which contains albedo, normal and position
                    //todo: generate Surfels




                    //
                }
            }

            dirtChunkId.Clear();
            dirtChunks.Clear();


            info.rt0.Release();
            info.rt1.Release();
            info.rt2.Release();
            info.rt3.Release();
            GameObject.DestroyImmediate(go);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void OnDrawGizmos()
        {
            if (!showDirtArea) return;
            if (dirtChunks == null) return;
            using (new GizmosHelper())
            {
                Gizmos.color = Color.red;
                foreach (var id in dirtChunkId)
                {
                    Gizmos.DrawWireCube(new Vector3(id.x * 64 + 32, 0, id.y * 64 + 32), new Vector3(64, 16, 64));
                }
            }
        }

#endif

        // Start is called before the first frame update
        void Start()
        {
#if UNITY_EDITOR
            if (dirtChunks.Count != 0) {
                Debug.LogError("Need rebake scene probes");
            }
#endif



        }

        // Update is called once per frame
        void Update()
        {

        }
    }

}

