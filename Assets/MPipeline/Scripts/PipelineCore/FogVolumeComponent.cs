using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [ExecuteInEditMode]
    public unsafe sealed class FogVolumeComponent : MonoBehaviour
    {
        public struct FogVolumeContainer
        {
            public FogVolume volume;
            public void* light;
        }

        public static NativeList<FogVolumeContainer> allVolumes;
        private int index = 0;
        public float volume = 1;
        public Color fogColor = Color.white;
        public Color emissionColor = Color.black;
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        }
        private void OnEnable()
        {
            if (!allVolumes.isCreated)
                allVolumes = new NativeList<FogVolumeContainer>(30, Allocator.Persistent);

            FogVolumeContainer currentcon;
            currentcon.light = MUnsafeUtility.GetManagedPtr(this);
            float3x3 localToWorld = new float3x3
            {
                c0 = transform.right,
                c1 = transform.up,
                c2 = transform.forward
            };
            float4x4 worldToLocal = transform.worldToLocalMatrix;
            FogVolume volume = new FogVolume
            {
                extent = transform.localScale * 0.5f,
                localToWorld = localToWorld,
                position = transform.position,
                worldToLocal = float3x4(worldToLocal.c0.xyz, worldToLocal.c1.xyz, worldToLocal.c2.xyz, worldToLocal.c3.xyz),
                targetVolume = this.volume,
                color = float3(fogColor.r, fogColor.g, fogColor.b),
                emissionColor = float3(emissionColor.r, emissionColor.g, emissionColor.b)
            };
            currentcon.volume = volume;
            allVolumes.Add(currentcon);
            index = allVolumes.Length - 1;
        }

        [EasyButtons.Button]
        public void UpdateVolume()
        {
            float3x3 localToWorld = new float3x3
            {
                c0 = transform.right,
                c1 = transform.up,
                c2 = transform.forward
            };
            float4x4 worldToLocal = transform.worldToLocalMatrix;
            FogVolume volume = new FogVolume
            {
                extent = transform.localScale * 0.5f,
                localToWorld = localToWorld,
                position = transform.position,
                worldToLocal = float3x4(worldToLocal.c0.xyz, worldToLocal.c1.xyz, worldToLocal.c2.xyz, worldToLocal.c3.xyz),
                targetVolume = this.volume,
                color = float3(fogColor.r, fogColor.g, fogColor.b),
                emissionColor = float3(emissionColor.r, emissionColor.g, emissionColor.b)
            };
            allVolumes[index].volume = volume;
        }


        private void OnDisable()
        {
            allVolumes[index] = allVolumes[allVolumes.Length - 1];
            FogVolumeComponent lastComp = MUnsafeUtility.GetObject<FogVolumeComponent>(allVolumes[index].light);
            lastComp.index = index;
            allVolumes.RemoveLast();
        }
    }
}