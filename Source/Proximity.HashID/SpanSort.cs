using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Proximity.HashID.Tests")]

namespace Proximity.HashID
{
    internal static class SpanSort
    {
        internal static void Sort<T>(this Span<T> target) where T : IComparable<T>
        {
            if (target.IsEmpty || target.Length == 1)
                return;
            var Offset = Partition(target);
            Sort(target.Slice(0, Offset++));
            if (Offset < target.Length - 1) // not already sorted
                Sort(target.Slice(Offset));
        }

        private static int Partition<T>(Span<T> segment) where T : IComparable<T>
        {
            ref var Pivot = ref segment[segment.Length - 1];
            var Left = -1; // current end of lessThan array part
            for (var Right = 0; Right < segment.Length - 1; Right++)
            {
                if (segment[Right].CompareTo(Pivot) == -1)
                {
                    Left++;
                    (segment[Left], segment[Right]) = (segment[Right], segment[Left]);
                }
            }

            var Offset = Left + 1; //pivotPosition

            (segment[Offset], Pivot) = (Pivot, segment[Offset]);

            return Offset;
        }
    }
}
