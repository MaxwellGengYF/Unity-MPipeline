using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public interface IPipelineRunnable
    {
        void PipelineUpdate(ref PipelineCommandData data);
    }
}