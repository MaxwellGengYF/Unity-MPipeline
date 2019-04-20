using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using static Unity.Mathematics.math;
using MPipeline;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Mathematics;
using Unity.Collections;
using Random = UnityEngine.Random;
public unsafe class Test : MonoBehaviour
{
    /*  public UnityEngine.UI.Text txt;
      private float deltaAcc = 0;
      private int count = 0;
      private void Update()
      {
          deltaAcc += Time.deltaTime;
          count++;
          if (count >= 20)
          {
              deltaAcc /= count;
              txt.text = (deltaAcc * 1000).ToString();
              count = 0;
              deltaAcc = 0;
          }
      }*/
    public GameObject sphere;
    [EasyButtons.Button]
    void Run()
    {
        for(int i = 0; i < 1000; ++i)
        {
            Transform newTrans = (Instantiate(sphere) as GameObject).transform;
            float3 pos = VectorUtility.GetSphereRandom(VectorUtility.Hammersley((uint)i, 1000)) * 100;
            newTrans.position = pos;
        }
    }
}
