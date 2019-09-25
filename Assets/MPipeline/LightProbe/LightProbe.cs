using UnityEditor;
using UnityEngine;

namespace MPipeline
{
    class LightProbe : MonoBehaviour
    {
        [SerializeField]
        public Vector3[] probePositions = new Vector3[0];

        [SerializeField]
        public float cellSize = 4;

        [SerializeField]
        public Vector3 volumeSize = new Vector3(4, 4, 4);

        [SerializeField]
        public bool showVolumeInScene = true;


        public void RecalcuMesh()
        {

        }

        private void OnDrawGizmos()
        {
            if (!showVolumeInScene) return;
            GizmosHelper gz = new GizmosHelper();
            gz.Init();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, volumeSize);
            Gizmos.color = new Color(0.5f, 0, 0.2f, 0.3f);
            Gizmos.DrawCube(Vector3.zero, volumeSize);
            gz.Dispose();
        }
    }
}


