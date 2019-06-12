using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MPipeline
{
    internal class LPSceneFile : ScriptableObject
    {
        [System.Serializable]
        class ChunkList {
            [SerializeField]
            public List<Vector2Int> id;
            [SerializeField]
            public List<LPChunk> files;
        }

        [SerializeField]
        ChunkList[] chunkLists;

        [SerializeField]
        string sceneName;

        public LPChunk GetChunkFile(Vector2Int id)
        {
            int index = HashId(id);

            ChunkList list = chunkLists[index];

            if (list.id == null)
            {
                list.id = new  List<Vector2Int>();
                list.files = new List<LPChunk>();
                chunkLists[index] = list;
                EditorUtility.SetDirty(this);
            }

            LPChunk file = null;
            for (int i = 0; i < list.id.Count; i++)
            {
                if (list.id[i] == id)
                {
                    file = list.files[i];
                    break;
                }
            }
            if (file == null)
            {
                file = LPChunk.CreateAsset("Assets/Resources/GI/" + sceneName + "/" + id.ToString() + ".asset");
                list.id.Add(id);
                list.files.Add(file);
                EditorUtility.SetDirty(this);
            }
            AssetDatabase.SaveAssets();
            return file;
        }





        public static LPSceneFile CreateAsset(string sceneName)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/GI"))
                AssetDatabase.CreateFolder("Assets/Resources", "GI");
            string path = "Assets/Resources/GI/" + sceneName + "_LPSceneFile.asset";
            LPSceneFile asset = AssetDatabase.LoadAssetAtPath<LPSceneFile>(path);
            if (asset == null) {
                asset = CreateInstance<LPSceneFile>();
                asset.chunkLists = new ChunkList[32 * 32];
                asset.sceneName = sceneName;
                AssetDatabase.CreateAsset(asset, path);
                if (!AssetDatabase.IsValidFolder("Assets/Resources/GI/" + sceneName))
                    AssetDatabase.CreateFolder("Assets/Resources/GI", sceneName);
            }
            AssetDatabase.Refresh();
            return asset;
        }

        static int HashId(Vector2Int id) { return ((id.x + 9999) % 32) * 32 + (id.y + 9999) % 32; }
    }
}
