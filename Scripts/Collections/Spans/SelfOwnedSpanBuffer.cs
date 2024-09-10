using System;
using System.Buffers;

namespace Tools.Collections.Spans
{
    public readonly ref struct SelfOwnedSpanBuffer<T>
    {
        private readonly IMemoryOwner<T> _memoryOwner;

        public SpanBuffer<T> Buffer { get; }

        public SelfOwnedSpanBuffer(Span<T> buffer, int initialCount, IMemoryOwner<T> memoryOwner)
        {
            Buffer = new SpanBuffer<T>(buffer, initialCount);
            _memoryOwner = memoryOwner;
        }

        public void Dispose()
        {
            _memoryOwner.Dispose();
        }
    }
}