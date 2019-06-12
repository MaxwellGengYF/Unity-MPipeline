using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MPipeline
{
    internal class LPSystem
    {

    }


    internal class LPChunk
    {
        LPProbe[] probes;
        LPSurfel[] surfels;
        LPSurfelGroup[] surfelGroups;

        ComputeBuffer probeBuffer, surfelBuffer, groupBuffer;

        public void PrepairBuffer()
        {
            if (probes == null) {
                Debug.LogError("Error, not init");
                return;
            }
            //todo:
            
            //
        }

    }

    internal struct LPProbe
    {
        Vector3 position;
    }

    internal struct LPSurfel
    {
        Vector3 position, normal;
        Color albedo;
    }

    internal struct IdWeight
    {
        int id;
        float weight;
    }

    internal class LPSurfelGroup
    {
        int[] surfelId;
        IdWeight[] influncedProbeIdWeight;
    }

}