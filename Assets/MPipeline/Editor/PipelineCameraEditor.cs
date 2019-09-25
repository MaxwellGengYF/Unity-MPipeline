using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MPipeline;
[CustomEditor(typeof(PipelineCamera))]
public class PipelineCameraEditor : Editor
{
    bool foldOut = false;
    PipelineCamera target;
    MStringBuilder msb;
    private void OnEnable()
    {
        target = serializedObject.targetObject as PipelineCamera;
        msb = new MStringBuilder(50);
    }
    public override void OnInspectorGUI()
    {
        if(GUILayout.Button("Reset Matrix"))
        {
            target.ResetMatrix();
        }
        base.OnInspectorGUI();
        if (foldOut = EditorGUILayout.Foldout(foldOut, "Layer Culling Distance: "))
        {
            for (int i = 0; i < 32; ++i)
            {
                string layerName = LayerMask.LayerToName(i);

                if (!string.IsNullOrEmpty(layerName))
                {
                    msb.Combine("   ", layerName);
                    msb.Add(": ");
                    target.layerCullDistance[i] = EditorGUILayout.FloatField(msb.str, target.layerCullDistance[i]);
                }
            }

        }
    }
}
