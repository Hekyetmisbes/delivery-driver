using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrafficSystem;

namespace DeliveryDriver.Tests
{
    public class StableRoadPriorityQueueTests
    {
        [Test]
        public void EqualPriorities_AreDequeuedInInsertionOrder()
        {
            var queue = new StableRoadPriorityQueue();
            queue.Enqueue(1, 3f);
            queue.Enqueue(2, 1f);
            queue.Enqueue(3, 3f);
            queue.Enqueue(4, 1f);
            Assert.That(queue.Dequeue(), Is.EqualTo(2));
            Assert.That(queue.Dequeue(), Is.EqualTo(4));
            Assert.That(queue.Dequeue(), Is.EqualTo(1));
            Assert.That(queue.Dequeue(), Is.EqualTo(3));
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void InterleavedOperations_MatchPreviousSortedListQueue()
        {
            var actual = new StableRoadPriorityQueue();
            var reference = new SortedList<float, Queue<long>>();
            var random = new Random(42);
            for (long key = 0; key < 5000; key++)
            {
                float cost = random.Next(-10, 40) * 0.5f;
                actual.Enqueue(key, cost);
                if (!reference.TryGetValue(cost, out var bucket))
                {
                    bucket = new Queue<long>();
                    reference.Add(cost, bucket);
                }
                bucket.Enqueue(key);
                if (key % 3 == 0)
                    CompareNext(actual, reference);
            }
            while (reference.Count > 0)
                CompareNext(actual, reference);
            Assert.That(actual.Count, Is.Zero);
        }

        [Test]
        public void Clear_ResetsReusedQueue()
        {
            var queue = new StableRoadPriorityQueue();
            queue.Enqueue(9, 0f);
            queue.Clear();
            queue.Enqueue(5, 5f);
            Assert.That(queue.Dequeue(), Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
        }

        private static void CompareNext(StableRoadPriorityQueue actual, SortedList<float, Queue<long>> reference)
        {
            var first = reference.Values[0];
            long expected = first.Dequeue();
            if (first.Count == 0)
                reference.RemoveAt(0);
            Assert.That(actual.Dequeue(), Is.EqualTo(expected));
        }
    }
}
