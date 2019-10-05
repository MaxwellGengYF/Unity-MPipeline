#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.IO;
using MPipeline;
using UnityEditor;
public unsafe sealed class AnimationBaker : ScriptableWizard
{
    public Animator targetAnimator;
    public SkinnedMeshRenderer renderer;
    public string targetPath = "Assets/";
    [MenuItem("MPipeline/Create Animation")]
    private static void CreateWizard()
    {
        DisplayWizard<AnimationBaker>("Animation Baker", "Bake");
    }
    private float3 position;
    private quaternion rotation;
    private float3 scale;

    private void Initialize()
    {
        rotation = targetAnimator.transform.rotation;
        position = targetAnimator.transform.position;
        scale = targetAnimator.transform.localScale;
        targetAnimator.transform.rotation = Quaternion.identity;
        targetAnimator.transform.position = Vector3.zero;
        targetAnimator.transform.localScale = Vector3.one;
    }

    private void Finialize()
    {
        targetAnimator.transform.rotation = rotation;
        targetAnimator.transform.position = position;
        targetAnimator.transform.localScale = scale;
    }

    private byte[] ProcessSingleClip(AnimationClip clip, Transform[] bones)
    {
        int allFrame = (int)(clip.length * clip.frameRate);
        byte[] results = new byte[allFrame * sizeof(float3x4) * bones.Length + sizeof(AnimationHead)];
        AnimationHead* headPtr = (AnimationHead*)results.Ptr();
        headPtr->frameRate = clip.frameRate;
        headPtr->length = clip.length;
        headPtr->bonesCount = bones.Length;
        Debug.Log("FrameRate: " + clip.frameRate);
        Debug.Log("Length: " + clip.length);
        float3x4* bonesPtr = (float3x4*)(headPtr + 1);
        for(int i = 0; i < allFrame; ++i)
        {
            clip.SampleAnimation(targetAnimator.gameObject, i / clip.frameRate);
            for(int j = 0; j < bones.Length; ++j)
            {
                float4x4 mat = bones[j].localToWorldMatrix;
                (*bonesPtr) = float3x4(mat.c0.xyz, mat.c1.xyz, mat.c2.xyz, mat.c3.xyz);
                bonesPtr++;
            }
        }
        return results;
    }

    private void OnWizardCreate()
    {
        Initialize();
        AnimationClip[] clips = targetAnimator.runtimeAnimatorController.animationClips;
        Transform[] bones = renderer.bones;
        foreach(var i in clips)
        {
            byte[] bytes = ProcessSingleClip(i, bones);
            File.WriteAllBytes(targetPath + i.name + ".bytes", bytes);
        }
        
        Finialize();
    }

}
#endif