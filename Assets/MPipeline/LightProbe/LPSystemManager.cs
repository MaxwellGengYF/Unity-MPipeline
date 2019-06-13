using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        struct ShaderPropertiesID
        {
            public static int ProbePosition = Shader.PropertyToID("ProbePosition");
            public static int Cube0 = Shader.PropertyToID("Cube0");
            public static int Cube1 = Shader.PropertyToID("Cube1");
            public static int ResultCount = Shader.PropertyToID("ResultCount");
            public static int Result = Shader.PropertyToID("Result"); 
        }


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

        public LPChunk GetChunk(Vector2Int id)
        {
            if (sceneLightProbeFile != null)
                return sceneLightProbeFile.GetChunk(id);
            return null;
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
            ComputeBuffer cb_Result = new ComputeBuffer(64 * 64 * 64, Marshal.SizeOf<LPSurfel>());
            ComputeBuffer cb_ResultCount = new ComputeBuffer(1, sizeof(int));


            for (int i = 0; i < dirtChunkId.Count; i++)
            {
                Vector2Int id = dirtChunkId[i];

                List<LPProbe> probesFromLightProbes = new List<LPProbe>();
                List<LPProbe> probes = new List<LPProbe>();
                Dictionary<Vector3Int, List<LPSurfel>> surfels_countainslist = new Dictionary<Vector3Int, List<LPSurfel>>();
                Dictionary<Vector3Int, LPSurfel> surfels = new Dictionary<Vector3Int, LPSurfel>();
                List<LPSurfel> surfels_list = new  List<LPSurfel>();
                Dictionary<Vector3Int, List<int>> surfel_probe = new Dictionary<Vector3Int, List<int>>();
                List<LPSurfelGroup> groups = new List<LPSurfelGroup>();

                //get probes from LightProbes in scene
                {
                    Bounds bound = new Bounds(new Vector3(id.x * 64 + 32, 0, id.y * 64 + 32), new Vector3(64, 999999999, 64));
                    foreach (var group in dirtChunks[i].pgs)
                    {
                        foreach (var localPos in group.probePositions)
                        {
                            Vector3 worldPos = group.transform.TransformPoint(localPos);
                            if (bound.Contains(worldPos))
                            {
                                probesFromLightProbes.Add(new LPProbe(worldPos));
                            }
                        }
                    }
                }

                //per probe get surfel data
                for (int j = 0; j < probesFromLightProbes.Count; j++)
                {
                    Vector3 porbePosition = probesFromLightProbes[j].position;

                    cam.transform.position = porbePosition;
                    cam.Render();
                    
                    cs_GetSurfelFromGBuffer.SetVector(ShaderPropertiesID.ProbePosition, porbePosition);
                    cs_GetSurfelFromGBuffer.SetTexture(0, ShaderPropertiesID.Cube0, info.rt0);
                    cs_GetSurfelFromGBuffer.SetTexture(0, ShaderPropertiesID.Cube1, info.rt1);

                    cb_ResultCount.SetData(new int[] { 0 });
                    cs_GetSurfelFromGBuffer.SetBuffer(0, ShaderPropertiesID.ResultCount, cb_ResultCount);
                    cs_GetSurfelFromGBuffer.SetBuffer(0, ShaderPropertiesID.Result, cb_Result);

                    cs_GetSurfelFromGBuffer.Dispatch(0, 8, 8, 8);

                    int[] count = new int[1];
                    cb_ResultCount.GetData(count);

                    if (count[0] <= 0)
                        continue;

                    //add into probes
                    probes.Add(probesFromLightProbes[j]);
                    int probeId = probes.Count - 1;

                    LPSurfel[] surfelsReadbackFromGPU = new LPSurfel[count[0]];
                    cb_Result.GetData(surfelsReadbackFromGPU, 0, 0, count[0]);

                    //add to surfel dictionary
                    foreach (var surfel in surfelsReadbackFromGPU)
                    {
                        List<LPSurfel> surfel_list;
                        Vector3Int surfelPositionInt = VectorToVectorInt(surfel.position);
                        if (surfel_probe.ContainsKey(surfelPositionInt))
                        {
                            surfel_probe[surfelPositionInt].Add(probeId);
                        }
                        else
                        {
                            surfel_probe.Add(surfelPositionInt, new List<int>(new int[] { probeId }));
                        }
                        if (surfels_countainslist.ContainsKey(surfelPositionInt))
                            surfel_list = surfels_countainslist[surfelPositionInt];
                        else {
                            surfel_list = new List<LPSurfel>();
                            surfels_countainslist.Add(surfelPositionInt, surfel_list);
                        }
                        surfel_list.Add(surfel);
                    }
                }

                //calculate average of surfel list of each surfel
                foreach (var surfel_list in surfels_countainslist)
                {
                    LPSurfel surfel = new LPSurfel();
                    foreach (var surfel_info in surfel_list.Value)
                    {
                        surfel.position += surfel_info.position;
                        surfel.normal += surfel_info.normal;
                        surfel.albedo += surfel_info.albedo;
                    }
                    surfel.position /= surfel_list.Value.Count;
                    surfel.normal.Normalize();
                    surfel.albedo /= surfel_list.Value.Count;

                    surfels.Add(surfel_list.Key, surfel);
                }

                //generate surfel groups
                {
                    Vector3Int minP = new Vector3Int(99999999, 99999999, 99999999), maxP = new Vector3Int(-99999999, -99999999, -99999999);
                    foreach (var surfel in surfels) {
                        minP = Vector3Int.Min(surfel.Key, minP);
                        maxP = Vector3Int.Max(surfel.Key, maxP);
                    }
                    for (int j = minP.x; j <= maxP.x; j+=4)
                        for (int k = minP.y; k <= maxP.y; k+=4)
                            for (int m = minP.z; m <= maxP.z; m+=4)
                            {
                                LPSurfelGroup group = new LPSurfelGroup();
                                List<int> surfel_ids = new List<int>();
                                List<IdWeight> idWeight = new List<IdWeight>();
                                Dictionary<int, int> probe_id_num = new Dictionary<int, int>();
                                for (int n = 0; n < 4; n++)
                                    for (int p = 0; p < 4; p++)
                                        for (int q = 0; q < 4; q++)
                                        {
                                            Vector3Int surfelPositionInt = new Vector3Int(j + n, k + p, m + q);
                                            if (surfels.ContainsKey(surfelPositionInt))
                                            {
                                                surfels_list.Add(surfels[surfelPositionInt]);
                                                surfel_ids.Add(surfels_list.Count - 1);
                                                foreach (var probeId in surfel_probe[surfelPositionInt])
                                                {
                                                    if (probe_id_num.ContainsKey(probeId))
                                                        probe_id_num[probeId] += 1;
                                                    else
                                                        probe_id_num.Add(probeId, 1);
                                                }
                                            }
                                        }
                                if (surfel_ids.Count == 0) continue;
                                group.surfelId = surfel_ids.ToArray();
                                foreach (var id_num in probe_id_num)
                                {
                                    idWeight.Add(new IdWeight(id_num.Key, (float)id_num.Value / surfel_ids.Count));
                                }
                                group.influncedProbeIdWeight = idWeight.ToArray();
                                groups.Add(group);
                            }
                }
                
                //save to file
                {
                    LPChunk chunk = sceneLightProbeFile.GetChunkFile(id);
                    chunk.probes = probes.ToArray();
                    chunk.surfels = surfels_list.ToArray();
                    chunk.surfelGroups = groups.ToArray();
                    EditorUtility.SetDirty(chunk);
                }
            }

            dirtChunkId.Clear();
            dirtChunks.Clear();


            info.rt0.Release();
            info.rt1.Release();
            info.rt2.Release();
            info.rt3.Release();
            GameObject.DestroyImmediate(go);
            cb_Result.Release();

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        static Vector3Int VectorToVectorInt(Vector3 v) {
            return new Vector3Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
        }

        private void OnDrawGizmos()
        {
            if (!showDirtArea) return;
            if (dirtChunks == null)
            {
                dirtChunkId = new List<Vector2Int>();
                dirtChunks = new List<Chunk_pg>();
            }
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

