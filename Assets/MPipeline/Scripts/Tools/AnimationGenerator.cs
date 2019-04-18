#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using Unity.Collections;
using MPipeline;
public unsafe class AnimationGenerator : ScriptableWizard
{
    public string animationPath = "Assets/AnimationTest";
    public string bindedMeshPath = "Assets/BindPoses";
    public string animationName;
    public float animationFPS = 30;
    public SkinnedMeshRenderer skinMeshRender;
    public Animation animation;
    [MenuItem("MPipeline/Generate Curve")]
    private static void CreateWizard()
    {
        DisplayWizard<AnimationGenerator>("Animation Generator", "Close", "Generate");
    }

    private void SaveAnimation()
    {
        Transform parent = animation.transform;
        animation.Play();
        AnimationState state = animation[animationName];
        int frameCount = (int)(state.length * animationFPS);
        float slice = 1 / animationFPS;
        int frameIte = 0;
        float currentTime = 0;
        Transform[] bones = skinMeshRender.bones;
        Texture2D animTex = new Texture2D(frameCount, bones.Length * 3, TextureFormat.RGBAFloat, false, true);
        while (frameIte < frameCount)
        {
            state.time = currentTime;
            animation.Sample();
            for (int i = 0; i < bones.Length; ++i)
            {
                Matrix4x4 matrix = parent.worldToLocalMatrix * bones[i].localToWorldMatrix;
                Vector4 colorVec = matrix.GetRow(0);
                animTex.SetPixel(frameIte, 3 * i, new Color(colorVec.x, colorVec.y, colorVec.z, colorVec.w));
                colorVec = matrix.GetRow(1);
                animTex.SetPixel(frameIte, 3 * i + 1, new Color(colorVec.x, colorVec.y, colorVec.z, colorVec.w));
                colorVec = matrix.GetRow(2);
                animTex.SetPixel(frameIte, 3 * i + 2, new Color(colorVec.x, colorVec.y, colorVec.z, colorVec.w));
            }
            frameIte += 1;
            currentTime += slice;
        }
        animTex.Apply();
        AssetDatabase.CreateAsset(animTex, animationPath + ".asset");
    }

    private void OnWizardOtherButton()
    {
        SaveAnimation();
    }
}
#endif