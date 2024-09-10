using System;
using System.Buffers;

namespace Tools.Collections.Memory
{
    public readonly ref struct MemoryWithOwnership<T>
    {
        public readonly IMemoryOwner<T> Owner;
        public readonly Memory<T> Memory;

        public MemoryWithOwnership(IMemoryOwner<T> owner, Memory<T> memory)
        {
            Owner = owner;
            Memory = memory;
        }
    }
}