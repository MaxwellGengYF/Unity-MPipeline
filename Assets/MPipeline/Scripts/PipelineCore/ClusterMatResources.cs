using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    [System.Serializable]
    public struct TexturePaths
    {
        public string texName;
        public string[] instancingIDs;
    }
    [System.Serializable]
    public struct LightmapPaths
    {
        public string name;
        public int size;
    }
    [System.Serializable]
    public struct ClusterProperty
    {
        public string name;
        public int clusterCount;
    }
    public class ClusterMatResources : ScriptableObject
    {
        public List<ClusterProperty> clusterProperties;
    }   
}
