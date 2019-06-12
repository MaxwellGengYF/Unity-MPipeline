using UnityEditor;
using UnityEngine;

namespace MPipeline
{
    class LightProbeVolmue : MonoBehaviour
    {
        [SerializeField]
        public Vector3[] probePositions = new Vector3[0];

        [SerializeField]
        public float cellSize = 4;

        [SerializeField]
        public Vector3 volumeSize = new Vector3(4,4,4);

        [SerializeField]
        public bool showVolumeInScene = true;

        private void OnDrawGizmos()
        {
            if (!showVolumeInScene) return;
            using (new GizmosHelper())
            {
                Gizmos.matrix =transform.localToWorldMatrix;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(Vector3.zero, volumeSize);
                Gizmos.color = new Color(0.5f, 0, 0.2f, 0.2f);
                Gizmos.DrawCube(Vector3.zero, volumeSize);
            }
        }

#if UNITY_EDITOR

        [MenuItem("GameObject/Light/Light probe volume")]
        static void CreateLightPobes()
        {
            GameObject go = new GameObject();
            go.AddComponent<LightProbeVolmue>();
            go.transform.position = SceneView.lastActiveSceneView.camera.transform.position + SceneView.lastActiveSceneView.camera.transform.forward * 10;
            go.name = "LightProbeVolume";
            Selection.objects = new Object[] { go };
        }

#endif
    }
}


