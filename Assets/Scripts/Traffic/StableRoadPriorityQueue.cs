using System;
using System.Collections.Generic;

namespace TrafficSystem
{
    /// <summary>Reusable min-heap preserving FIFO order for equal A* priorities.</summary>
    public sealed class StableRoadPriorityQueue
    {
        private readonly List<Entry> heap = new List<Entry>(256);
        private ulong nextSequence;

        private readonly struct Entry
        {
            public readonly long Key;
            public readonly float Cost;
            public readonly ulong Sequence;

            public Entry(long key, float cost, ulong sequence)
            {
                Key = key;
                Cost = cost;
                Sequence = sequence;
            }
        }

        public int Count => heap.Count;

        public void Clear()
        {
            heap.Clear();
            nextSequence = 0;
        }

        public void Enqueue(long key, float cost)
        {
            var entry = new Entry(key, cost, nextSequence++);
            int index = heap.Count;
            heap.Add(entry);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!Less(entry, heap[parent]))
                    break;

                heap[index] = heap[parent];
                index = parent;
            }
            heap[index] = entry;
        }

        public long Dequeue()
        {
            if (heap.Count == 0)
                throw new InvalidOperationException("Road priority queue is empty.");

            long result = heap[0].Key;
            int last = heap.Count - 1;
            Entry tail = heap[last];
            heap.RemoveAt(last);
            if (last == 0)
                return result;

            int index = 0;
            while (index < heap.Count / 2)
            {
                int child = index * 2 + 1;
                int right = child + 1;
                if (right < heap.Count && Less(heap[right], heap[child]))
                    child = right;

                if (!Less(heap[child], tail))
                    break;

                heap[index] = heap[child];
                index = child;
            }
            heap[index] = tail;
            return result;
        }

        private static bool Less(Entry a, Entry b)
        {
            int order = a.Cost.CompareTo(b.Cost);
            return order < 0 || (order == 0 && a.Sequence < b.Sequence);
        }
    }
}
