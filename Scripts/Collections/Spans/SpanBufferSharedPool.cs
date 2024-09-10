using System;
using System.Buffers;

namespace Tools.Collections.Spans
{
    public static class SpanBufferSharedPool
    {
        public static SelfOwnedSpanBuffer<T> RentBuffer<T>(int maxVerts, out SpanBuffer<T> spanBuffer)
        {
            var memoryOwner = MemoryPool<T>.Shared.Rent(maxVerts);
            Span<T> verts = memoryOwner.Memory.Slice(0, maxVerts).Span;

            var selfOwnedBuffer = new SelfOwnedSpanBuffer<T>(verts, 0, memoryOwner);
            spanBuffer = selfOwnedBuffer.Buffer;
            
            return selfOwnedBuffer;
        }
    }
}