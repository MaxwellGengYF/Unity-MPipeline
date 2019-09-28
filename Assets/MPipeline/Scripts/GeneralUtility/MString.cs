using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Collections;
using System.Runtime.CompilerServices;
public unsafe struct MString
{
    public char* chr { get; private set; }
    private Allocator alloc;
    public int Length { get; private set; }
    private int hashCode;
    public MString(string str, Allocator alloc)
    {
        Length = str.Length;
        this.alloc = alloc;
        int stride = str.Length * sizeof(char);
        chr = (char*)Malloc(stride, 16, alloc);
        fixed (char* charFromStr = str)
        {
            MemCpy(chr, charFromStr, stride);
        }
        hashCode = str.GetHashCode();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return 0;
    }

    public ref char this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref chr[index];
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Free(chr, alloc);
    }
}
