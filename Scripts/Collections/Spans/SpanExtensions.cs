using System;

namespace Tools.Collections.Spans
{
    public static class SpanExtensions
    {
        public static (TVert item, TVert prevItem) GetCircularConsecutivePair<TVert>(this Span<TVert> items, int index)
        {
            return GetCircularConsecutivePair((ReadOnlySpan<TVert>)items, index);
        }
        
        public static (TVert item, TVert prevItem) GetCircularConsecutivePair<TVert>(this ReadOnlySpan<TVert> items, int index)
        {
            var i = index % items.Length;
            var prevI = index - 1;
            prevI = prevI < 0 ? items.Length + prevI : prevI;

            return (items[i], items[prevI]);
        }
        
        public static (TVert item, TVert prevItem, TVert nextItem) GetCircularConsecutiveTrio<TVert>(this Span<TVert> items, int index)
        {
            return GetCircularConsecutiveTrio((ReadOnlySpan<TVert>)items, index);
        }
        
        public static (TVert item, TVert prevItem, TVert nextItem) GetCircularConsecutiveTrio<TVert>(this ReadOnlySpan<TVert> items, int index)
        {
            var i = index % items.Length;
            var prevI = index - 1;
            prevI = prevI < 0 ? items.Length + prevI : prevI;
            var nextI = index + 1;
            nextI = nextI > items.Length - 1 ? items.Length % nextI : nextI;

            return (items[i], items[prevI], items[nextI]);
        }
    }
}