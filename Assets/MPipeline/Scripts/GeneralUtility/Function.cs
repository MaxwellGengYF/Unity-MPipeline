namespace Functional
{
    public delegate void Function();
    public delegate void Function<T>(ref T arg);
    public delegate void Function<T, B>(ref T arg0, ref B arg1);
    public delegate void Function<T, B, A>(ref T arg0, ref B arg1, ref A arg2);
    public delegate void Function<T, B, A, C>(ref T arg0, ref B arg1, ref A arg2, ref C arg3);
    public delegate void Function<T, B, A, C, D>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4);
    public delegate void Function<T, B, A, C, D, E>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5);
    public delegate void Function<T, B, A, C, D, E, F>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6);
    public delegate void Function<T, B, A, C, D, E, F, G>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7);
    public delegate void Function<T, B, A, C, D, E, F, G, H>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8);
    public delegate void Function<T, B, A, C, D, E, F, G, H, I>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8, ref I arg9);
    public delegate void Function<T, B, A, C, D, E, F, G, H, I, J>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8, ref I arg9, ref J arg10);
    public delegate void Function<T, B, A, C, D, E, F, G, H, I, J, K>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8, ref I arg9, ref J arg10, ref K arg11);
    public delegate void Function<T, B, A, C, D, E, F, G, H, I, J, K, L>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8, ref I arg9, ref J arg10, ref K arg11, ref L arg12);
    public delegate void Function<T, B, A, C, D, E, F, G, H, I, J, K, L, M>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8, ref I arg9, ref J arg10, ref K arg11, ref L arg12, ref M arg13);
    public delegate void Function<T, B, A, C, D, E, F, G, H, I, J, K, L, M, N>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8, ref I arg9, ref J arg10, ref K arg11, ref L arg12, ref M arg13, ref N arg14);
    public delegate void Function<T, B, A, C, D, E, F, G, H, I, J, K, L, M, N, O>(ref T arg0, ref B arg1, ref A arg2, ref C arg3, ref D arg4, ref E arg5, ref F arg6, ref G arg7, ref H arg8, ref I arg9, ref J arg10, ref K arg11, ref L arg12, ref M arg13, ref N arg14, ref O arg15);
}