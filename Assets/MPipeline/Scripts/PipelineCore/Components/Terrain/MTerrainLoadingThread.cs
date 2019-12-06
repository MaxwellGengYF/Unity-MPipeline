using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
namespace MPipeline
{
    public sealed class MTerrainLoadingThread
    {
        private Thread t;
        private AutoResetEvent resetEvent;
        private List<Action> missions;
        private bool enable;
        public MTerrainLoadingThread(int missionCapacity)
        {
            enable = true;
            missions = new List<Action>(missionCapacity);
            resetEvent = new AutoResetEvent(true);
            t = new Thread(() =>
            {
                while (enable)
                {
                    resetEvent.WaitOne();

                    foreach (var i in missions)
                    {
                        i();
                    }
                    missions.Clear();

                }
            });
            t.Start();
        }
        public void AddMission(Action a)
        {
            missions.Add(a);
        }
        public void Schedule()
        {
            resetEvent.Set();
        }
        public void Dispose()
        {
            enable = false;
            missions.Clear();
            resetEvent.Set();
            t.Join();
            t = null;
            resetEvent.Dispose();

        }
    }
}