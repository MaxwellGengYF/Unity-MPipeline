using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/LPEvent")]
    [RequireEvent(typeof(LightingEvent))]
    internal class LPEvent : PipelineEvent
    {
        private LPSystemManager m_SceneLightProbeSysyemManager = null;

        private bool m_HasSceneLightProbeSysyemManager = false;

        class ComputeBufferForOneChunk
        {
            public ComputeBuffer probes, surfels, surfelGroups, influncedGroupIdWeight;
            public ComputeBuffer surfelResult, shadowCache, surfelGroupResult, probeResult;

            private struct ShaderPropertiesID
            {
                public static int probes = Shader.PropertyToID("probes");
                public static int surfels = Shader.PropertyToID("surfels");
                public static int surfelGroups = Shader.PropertyToID("surfelGroups");
                public static int influncedGroupIdWeight = Shader.PropertyToID("influncedGroupIdWeight");
                public static int surfelResult = Shader.PropertyToID("surfelResult");
                public static int shadowCache = Shader.PropertyToID("shadowCache");
                public static int surfelGroupResult = Shader.PropertyToID("surfelGroupResult");
                public static int probeResult = Shader.PropertyToID("probeResult");
            }

            public ComputeBufferForOneChunk(LPChunk chunk)
            {
                probes = new ComputeBuffer(chunk.probes.Length, Marshal.SizeOf<LPProbe>());
                probes.SetData(chunk.probes);
                surfels = new ComputeBuffer(chunk.surfels.Length, Marshal.SizeOf<LPSurfel>());
                surfels.SetData(chunk.surfels);
                surfelGroups = new ComputeBuffer(chunk.surfelGroups.Length, Marshal.SizeOf<LPSurfelGroup>());
                surfelGroups.SetData(chunk.surfelGroups);
                influncedGroupIdWeight = new ComputeBuffer(chunk.influncedGroupIdWeight.Length, Marshal.SizeOf<IdWeight>());
                influncedGroupIdWeight.SetData(chunk.influncedGroupIdWeight);
                surfelResult = new ComputeBuffer(surfels.count, Marshal.SizeOf<Vector3>());
                shadowCache = new ComputeBuffer(surfels.count, sizeof(int));
                surfelGroupResult = new ComputeBuffer(surfels.count, Marshal.SizeOf<Vector3>());
                probeResult = new ComputeBuffer(surfels.count, Marshal.SizeOf<Vector3>() * 6);
            }

            public void BindToComputeShader(CommandBuffer cb, ComputeShader cs)
            {
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.probes, probes);
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.surfels, surfels);
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.surfelGroups, surfelGroups);
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.influncedGroupIdWeight, influncedGroupIdWeight);
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.surfelResult, surfelResult);
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.shadowCache, shadowCache);
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.surfelGroupResult, surfelGroupResult);
                cb.SetComputeBufferParam(cs, 0, ShaderPropertiesID.probeResult, probeResult);
            }
            ~ComputeBufferForOneChunk()
            {
                probes.Dispose();
                surfels.Dispose();
                surfelGroups.Dispose();
                influncedGroupIdWeight.Dispose();
                surfelResult.Dispose();
                shadowCache.Dispose();
                surfelGroupResult.Dispose();
                probeResult.Dispose();
                Debug.Log("Dispose cbs");
            }
        }

        class LightVolumeTextures
        {
            RenderTexture rt0, rt1, rt2, rt3, rt4, rt5;
        }

        Dictionary<Vector2Int, ComputeBufferForOneChunk> m_chunks;


        public override bool CheckProperty()
        {
            return m_SceneLightProbeSysyemManager != null;
        }

        protected override void Dispose()
        {
            m_chunks.Clear();
        }

        protected void Init()
        {
            try
            {
                m_SceneLightProbeSysyemManager = GameObject.Find("LPSystem").GetComponent<LPSystemManager>();
            }
            catch (System.Exception) { return;}

            if (m_SceneLightProbeSysyemManager == null) {

                m_HasSceneLightProbeSysyemManager = false;
                return;
            }
            m_chunks = new Dictionary<Vector2Int, ComputeBufferForOneChunk>();
        }

        protected override void Init(PipelineResources resources) {
            Init();
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (!m_HasSceneLightProbeSysyemManager) return;

            PrepairChunks(cam.transform.position, ref data);



        }



        Vector2Int[] chunkIds = new Vector2Int[4];
        Vector2Int[] chunkOutOfRange = new Vector2Int[4];
        Vector2Int[] chunkInRangeAndHasProbe = new Vector2Int[4];
        private void PrepairChunks(Vector3 camPos, ref PipelineCommandData data)
        {
            Vector2Int centerChunk = new Vector2Int(Mathf.FloorToInt(camPos.x / 64), Mathf.FloorToInt(camPos.x / 64));
            bool r = camPos.x > (centerChunk.x * 64 + 32);
            bool u = camPos.z > (centerChunk.y * 64 + 32);

            chunkIds[0] = centerChunk;
            chunkIds[1] = centerChunk + Vector2Int.right * (r ? 1 : -1);
            chunkIds[2] = centerChunk + Vector2Int.up * (u ? 1 : -1);
            chunkIds[3] = chunkIds[1] + Vector2Int.up * (u ? 1 : -1);
            int num = 0;
            foreach (var pair in m_chunks)
            {
                bool flag = false;
                foreach (var id in chunkIds)
                {
                    if (pair.Key == id)
                    {
                        flag = true; break;
                    }
                }
                if (!flag) chunkOutOfRange[num++] = pair.Key;
            }
            for (int i = 0; i < num; i++)
            {
                m_chunks.Remove(chunkOutOfRange[i]);
            }
            num = 0;
            for (int i = 0; i < 4; i++)
            {
                if (m_chunks.ContainsKey(chunkIds[i]))
                {
                    chunkInRangeAndHasProbe[num++] = chunkIds[i];
                    continue;
                }
                var chunk = m_SceneLightProbeSysyemManager.GetChunk(chunkIds[i]);

                if (chunk != null)
                {
                    m_chunks.Add(chunkIds[i], new ComputeBufferForOneChunk(chunk));
                    chunkInRangeAndHasProbe[num++] = chunkIds[i];
                }
            }

            if (num == 0) return;
            else if (num == 1)
            {
                RelightChunk(m_chunks[chunkInRangeAndHasProbe[0]], ref data);
            }
            else if (num == 2)
            {
                RelightChunk(m_chunks[chunkInRangeAndHasProbe[0]], ref data);
                RelightChunk(m_chunks[chunkInRangeAndHasProbe[1]], ref data);
            }
            else
            {
                int centerIndex = -1;
                if (chunkInRangeAndHasProbe[0] == centerChunk)
                    centerIndex = 0;
                else
                    centerIndex = Random.Range(0, num);
                RelightChunk(m_chunks[chunkInRangeAndHasProbe[centerIndex]], ref data);
                RelightChunk(m_chunks[chunkInRangeAndHasProbe[(Random.Range(1, num) + centerIndex) % num]], ref data);
            }
        }

        private void RelightChunk(ComputeBufferForOneChunk chunkCb, ref PipelineCommandData data)
        {
            var cs = data.resources.shaders.relightProbes;
            chunkCb.BindToComputeShader(data.buffer, cs);

            int surfelNum = chunkCb.surfels.count;
            int dispatchNum = surfelNum / 64 + (surfelNum % 64 > 0 ? 1 : 0);

            data.buffer.DispatchCompute(cs, 0, dispatchNum, 1, 1);


        }



        

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }
    }

}