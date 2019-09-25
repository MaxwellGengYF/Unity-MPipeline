using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline.PCG
{
    [RequireComponent(typeof(MeshFilter))]
    public class VTDecal : MonoBehaviour
    {
        private MeshFilter filter;
        public Mesh sharedMesh
        {
            get
            {
                return filter.sharedMesh;
            }
        }
        public Texture2D albedoTex;
        public Texture2D normalTex;
        public Texture2D smoTex;
        public Texture2D opaqueMask;
        public Vector4 scaleOffset = new Vector4(1, 1, 0, 0);
        [Range(0f, 1f)]
        public float smoothness = 1;
        [Range(0f, 1f)]
        public float metallic = 1;
        [Range(0f, 1f)]
        public float occlusion = 1;
        [Range(0f, 1f)]
        public float opaque = 1;
        private void OnEnable()
        {
            filter = GetComponent<MeshFilter>();
        }

    }

#if UNITY_EDITOR
    [CustomEditor(typeof(VTDecal))]
    public class VTDecalEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Update Atlas"))
            {
                TerrainPainter.updateAtlas = true;
            }
        }
    }
#endif
}
