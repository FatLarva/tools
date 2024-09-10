using System;

namespace Tools.Collections.Spans
{
    public ref struct SpanBuffer<T>
    {
        private readonly Span<T> _buffer;
        private int _count;

        public SpanBuffer(Span<T> buffer, int initialCount)
        {
            _buffer = buffer;
            _count = initialCount;
        }

        public void Add(T item)
        {
            _buffer[_count++] = item;
        }
        
        public void Reset()
        {
            _count = 0;
        }

        public Span<T> FilledBufferSlice()
        {
            return _buffer.Slice(0, _count);
        }
    }
}