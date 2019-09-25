using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public abstract class JobProcessEvent : MonoBehaviour
    {
        public static List<JobProcessEvent> allEvents = new List<JobProcessEvent>();
        private int localIndex;
        private void OnEnable()
        {
            localIndex = allEvents.Count;
            allEvents.Add(this);
            OnEnableFunc();
        }

        private void OnDisable()
        {
            allEvents[localIndex] = allEvents[allEvents.Count - 1];
            allEvents[localIndex].localIndex = localIndex;
            allEvents.RemoveAt(allEvents.Count - 1);
            localIndex = -1;
            OnDisableFunc();
        }
        public abstract void PrepareJob();
        public abstract void FinishJob();
        protected virtual void OnEnableFunc() { }
        protected virtual void OnDisableFunc() { }
    }
}