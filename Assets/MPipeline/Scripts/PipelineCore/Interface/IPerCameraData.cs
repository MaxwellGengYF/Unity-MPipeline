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
        public static T GetProperty<T, R>(PipelineCamera camera, R runnable) where T : IPerCameraData where R : struct, IGetCameraData
        {
            int index;
            IPerCameraData data;
            if (!camera.allDatas.isCreated) camera.allDatas = new NativeDictionary<ulong, int, PtrEqual>(20, Unity.Collections.Allocator.Persistent, new PtrEqual());
            if (!camera.allDatas.Get((ulong)MUnsafeUtility.GetManagedPtr(typeof(T)), out index))
            {
                data = runnable.Run();
                index = MUnsafeUtility.HookObject(data);
                camera.allDatas.Add((ulong)MUnsafeUtility.GetManagedPtr(typeof(T)), index);
            }
            return MUnsafeUtility.GetHookedObject(index) as T;
        }

        public static void RemoveProperty<T>(PipelineCamera camera)
        {
            int index = camera.allDatas[(ulong)MUnsafeUtility.GetManagedPtr(typeof(T))];
            IPerCameraData data = MUnsafeUtility.GetHookedObject(index) as IPerCameraData;
            if (data != null)
            {
                data.DisposeProperty();
            }
            MUnsafeUtility.RemoveHookedObject(index);
        }

        public abstract void DisposeProperty();
    }
}
