using System;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public interface IGetCameraData
    {
        IPerCameraData Run();
    }
    public unsafe abstract class IPerCameraData
    {
        public static T GetProperty<T>(PipelineCamera camera, Func<T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if(!camera.allDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc();
                camera.allDatas.Add(typeof(T), data);
            }
            return (T)data;
        }

        public static void RemoveProperty<T>(PipelineCamera camera)
        {
            IPerCameraData data = camera.allDatas[typeof(T)];
            if (data != null)
            {
                data.DisposeProperty();
            }
            data = null;
        }

        public static T GetProperty<T>(PipelineCamera camera, Func<PipelineCamera, T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.allDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc(camera);
                camera.allDatas.Add(typeof(T), data);
            }
            return (T)data;
        }

        public static T GetProperty<T, R>(PipelineCamera camera, R runnable) where T : IPerCameraData where R : IGetCameraData 
        {
            IPerCameraData data;
            if (!camera.allDatas.TryGetValue(typeof(T), out data))
            {
                data = runnable.Run();
                camera.allDatas.Add(typeof(T), data);
            }
            return (T)data;
        }

        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineCamera, PipelineResources, T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.allDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc(camera,resource);
                camera.allDatas.Add(typeof(T), data);
            }
            return (T)data;
        }
        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineResources, T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.allDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc(resource);
                camera.allDatas.Add(typeof(T), data);
            }
            return (T)data;
        }
        public abstract void DisposeProperty();
    }
}
