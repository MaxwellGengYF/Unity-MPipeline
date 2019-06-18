using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MPipeline
{
    internal class LPChunk : ScriptableObject
    {
        [SerializeField]
        public LPProbe[] probes;
        [SerializeField]
        public LPSurfel[] surfels;
        [SerializeField]
        public LPSurfelGroup[] surfelGroups;
        [SerializeField]
        public IdWeight[] influncedGroupIdWeight;

        public static LPChunk CreateAsset(string path)
        {
            LPChunk asset = AssetDatabase.LoadAssetAtPath<LPChunk>(path);
            if (asset == null)
            {
                asset = CreateInstance<LPChunk>();
                AssetDatabase.CreateAsset(asset, path);
            }
            AssetDatabase.Refresh();
            return asset;
        }
    }
}