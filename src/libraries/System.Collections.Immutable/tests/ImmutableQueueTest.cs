// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace System.Collections.Immutable.Tests
{
    public class ImmutableQueueTest : SimpleElementImmutablesTestBase
    {
        private void EnqueueDequeueTestHelper<T>(params T[] items)
        {
            Assert.NotNull(items);

            ImmutableQueue<T> queue = ImmutableQueue<T>.Empty;
            int i = 0;
            foreach (T item in items)
            {
                ImmutableQueue<T> nextQueue = queue.Enqueue(item);
                Assert.NotSame(queue, nextQueue); //, "Enqueue returned this instead of a new instance.");
                Assert.Equal(i, queue.Count()); //, "Enqueue mutated the queue.");
                Assert.Equal(++i, nextQueue.Count());
                queue = nextQueue;
            }

            i = 0;
            foreach (T element in queue)
            {
                AssertAreSame(items[i++], element);
            }

            i = 0;
            foreach (T element in (System.Collections.IEnumerable)queue)
            {
                AssertAreSame(items[i++], element);
            }

            i = items.Length;
            foreach (T expectedItem in items)
            {
                T actualItem = queue.Peek();
                AssertAreSame(expectedItem, actualItem);
                ImmutableQueue<T> nextQueue = queue.Dequeue();
                Assert.NotSame(queue, nextQueue); //, "Dequeue returned this instead of a new instance.");
                Assert.Equal(i, queue.Count());
                Assert.Equal(--i, nextQueue.Count());
                queue = nextQueue;
            }
        }

        [Fact]
        public void EnumerationOrder()
        {
            ImmutableQueue<int> queue = ImmutableQueue<int>.Empty;

            // Push elements onto the backwards stack.
            queue = queue.Enqueue(1).Enqueue(2).Enqueue(3);
            Assert.Equal(1, queue.Peek());

            // Force the backwards stack to be reversed and put into forwards.
            queue = queue.Dequeue();

            // Push elements onto the backwards stack again.
            queue = queue.Enqueue(4).Enqueue(5);

            // Now that we have some elements on the forwards and backwards stack,
            // 1. enumerate all elements to verify order.
            Assert.Equal<int>(new[] { 2, 3, 4, 5 }, queue.ToArray());

            // 2. dequeue all elements to verify order
            var actual = new int[queue.Count()];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = queue.Peek();
                queue = queue.Dequeue();
            }
        }

        [Fact]
        public void GetEnumeratorText()
        {
            ImmutableQueue<int> queue = ImmutableQueue.Create(5);
            ImmutableQueue<int>.Enumerator enumeratorStruct = queue.GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => enumeratorStruct.Current);
            Assert.True(enumeratorStruct.MoveNext());
            Assert.Equal(5, enumeratorStruct.Current);
            Assert.False(enumeratorStruct.MoveNext());
            Assert.Throws<InvalidOperationException>(() => enumeratorStruct.Current);

            IEnumerator<int> enumerator = ((IEnumerable<int>)queue).GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal(5, enumerator.Current);
            Assert.False(enumerator.MoveNext());
            Assert.Throws<InvalidOperationException>(() => enumerator.Current);

            enumerator.Reset();
            Assert.Throws<InvalidOperationException>(() => enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal(5, enumerator.Current);
            Assert.False(enumerator.MoveNext());
            enumerator.Dispose();
        }

        [Fact]
        public void EnumeratorRecyclingMisuse()
        {
            ImmutableQueue<int> queue = ImmutableQueue.Create(5);
            IEnumerator<int> enumerator = ((IEnumerable<int>)queue).GetEnumerator();
            IEnumerator<int> enumeratorCopy = enumerator;
            Assert.True(enumerator.MoveNext());
            enumerator.Dispose();
            Assert.Throws<ObjectDisposedException>(() => enumerator.MoveNext());
            Assert.Throws<ObjectDisposedException>(() => enumerator.Reset());
            Assert.Throws<ObjectDisposedException>(() => enumerator.Current);

            // As pure structs with no disposable reference types inside it,
            // we have nothing to track across struct copies, and this just works.
            ////Assert.Throws<ObjectDisposedException>(() => enumeratorCopy.MoveNext());
            ////Assert.Throws<ObjectDisposedException>(() => enumeratorCopy.Reset());
            ////Assert.Throws<ObjectDisposedException>(() => enumeratorCopy.Current);
            enumerator.Dispose(); // double-disposal should not throw
            enumeratorCopy.Dispose();

            // We expect that acquiring a new enumerator will use the same underlying Stack<T> object,
            // but that it will not throw exceptions for the new enumerator.
            enumerator = ((IEnumerable<int>)queue).GetEnumerator();
            Assert.True(enumerator.MoveNext());
            Assert.Equal(queue.Peek(), enumerator.Current);
            enumerator.Dispose();
        }

        [Fact]
        public void EnqueueDequeueTest()
        {
            this.EnqueueDequeueTestHelper(new GenericParameterHelper(1), new GenericParameterHelper(2), new GenericParameterHelper(3));
            this.EnqueueDequeueTestHelper<GenericParameterHelper>();

            // interface test
            IImmutableQueue<GenericParameterHelper> queueInterface = ImmutableQueue.Create<GenericParameterHelper>();
            IImmutableQueue<GenericParameterHelper> populatedQueueInterface = queueInterface.Enqueue(new GenericParameterHelper(5));
            Assert.Equal(new GenericParameterHelper(5), populatedQueueInterface.Peek());
        }

        [Fact]
        public void DequeueOutValue()
        {
            ImmutableQueue<int> queue = ImmutableQueue<int>.Empty.Enqueue(5).Enqueue(6);
            int head;
            queue = queue.Dequeue(out head);
            Assert.Equal(5, head);
            ImmutableQueue<int> emptyQueue = queue.Dequeue(out head);
            Assert.Equal(6, head);
            Assert.True(emptyQueue.IsEmpty);

            // Also check that the interface extension method works.
            IImmutableQueue<int> interfaceQueue = queue;
            Assert.Same(emptyQueue, interfaceQueue.Dequeue(out head));
            Assert.Equal(6, head);
        }

        [Fact]
        public void ClearTest()
        {
            ImmutableQueue<GenericParameterHelper> emptyQueue = ImmutableQueue.Create<GenericParameterHelper>();
            AssertAreSame(emptyQueue, emptyQueue.Clear());
            ImmutableQueue<GenericParameterHelper> nonEmptyQueue = emptyQueue.Enqueue(new GenericParameterHelper(3));
            AssertAreSame(emptyQueue, nonEmptyQueue.Clear());

            // Interface test
            IImmutableQueue<GenericParameterHelper> queueInterface = nonEmptyQueue;
            AssertAreSame(emptyQueue, queueInterface.Clear());
        }

        [Fact]
        public void EqualsTest()
        {
            Assert.False(ImmutableQueue<int>.Empty.Equals(null));
            Assert.False(ImmutableQueue<int>.Empty.Equals("hi"));
            Assert.True(ImmutableQueue<int>.Empty.Equals(ImmutableQueue<int>.Empty));
            Assert.False(ImmutableQueue<int>.Empty.Enqueue(3).Equals(ImmutableQueue<int>.Empty.Enqueue(3)));
            Assert.False(ImmutableQueue<int>.Empty.Enqueue(5).Equals(ImmutableQueue<int>.Empty.Enqueue(3)));
            Assert.False(ImmutableQueue<int>.Empty.Enqueue(3).Enqueue(5).Equals(ImmutableQueue<int>.Empty.Enqueue(3)));
            Assert.False(ImmutableQueue<int>.Empty.Enqueue(3).Equals(ImmutableQueue<int>.Empty.Enqueue(3).Enqueue(5)));

            // Also be sure to compare equality of partially dequeued queues since that moves data to different fields.
            Assert.False(ImmutableQueue<int>.Empty.Enqueue(3).Enqueue(1).Enqueue(2).Dequeue().Equals(ImmutableQueue<int>.Empty.Enqueue(1).Enqueue(2)));
        }

        [Fact]
        public void PeekEmptyThrows()
        {
            Assert.Throws<InvalidOperationException>(() => ImmutableQueue<GenericParameterHelper>.Empty.Peek());
        }

        [Fact]
        public void DequeueEmptyThrows()
        {
            Assert.Throws<InvalidOperationException>(() => ImmutableQueue<GenericParameterHelper>.Empty.Dequeue());
        }

        [Fact]
        public void Create()
        {
            ImmutableQueue<int> queue = ImmutableQueue.Create<int>();
            Assert.True(queue.IsEmpty);

            queue = ImmutableQueue.Create(1);
            Assert.False(queue.IsEmpty);
            Assert.Equal(new[] { 1 }, queue);

            queue = ImmutableQueue.Create(1, 2);
            Assert.False(queue.IsEmpty);
            Assert.Equal(new[] { 1, 2 }, queue);

            queue = ImmutableQueue.Create((ReadOnlySpan<int>)new[] { 1, 2 });
            Assert.False(queue.IsEmpty);
            Assert.Equal(new[] { 1, 2 }, queue);

            queue = ImmutableQueue.CreateRange((IEnumerable<int>)new[] { 1, 2 });
            Assert.False(queue.IsEmpty);
            Assert.Equal(new[] { 1, 2 }, queue);

            queue = ImmutableQueue.CreateRange(new List<int> { 1, 2 });
            Assert.False(queue.IsEmpty);
            Assert.Equal(new[] { 1, 2 }, queue);

            AssertExtensions.Throws<ArgumentNullException>("items", () => ImmutableQueue.CreateRange((IEnumerable<int>)null));
            AssertExtensions.Throws<ArgumentNullException>("items", () => ImmutableQueue.Create((int[])null));
        }

        [Fact]
        public void Empty()
        {
            // We already test Create(), so just prove that Empty has the same effect.
            Assert.Same(ImmutableQueue.Create<int>(), ImmutableQueue<int>.Empty);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsDebuggerTypeProxyAttributeSupported))]
        public void DebuggerAttributesValid()
        {
            DebuggerAttributes.ValidateDebuggerDisplayReferences(ImmutableQueue.Create<int>());
            ImmutableQueue<string> queue = ImmutableQueue.Create("One", "Two");
            DebuggerAttributeInfo info = DebuggerAttributes.ValidateDebuggerTypeProxyProperties(queue);
            PropertyInfo itemProperty = info.Properties.Single(pr => pr.GetCustomAttribute<DebuggerBrowsableAttribute>().State == DebuggerBrowsableState.RootHidden);
            string[] items = itemProperty.GetValue(info.Instance) as string[];
            Assert.Equal(queue, items);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsDebuggerTypeProxyAttributeSupported))]
        public static void TestDebuggerAttributes_Null()
        {
            Type proxyType = DebuggerAttributes.GetProxyType(ImmutableQueue.Create<int>());
            TargetInvocationException tie = Assert.Throws<TargetInvocationException>(() => Activator.CreateInstance(proxyType, (object)null));
            Assert.IsType<ArgumentNullException>(tie.InnerException);
        }

        [Fact]
        public void PeekRef()
        {
            ImmutableQueue<int> queue = ImmutableQueue<int>.Empty
                .Enqueue(1)
                .Enqueue(2)
                .Enqueue(3);

            ref readonly int safeRef = ref queue.PeekRef();
            ref int unsafeRef = ref Unsafe.AsRef(safeRef);

            Assert.Equal(1, queue.PeekRef());

            unsafeRef = 4;

            Assert.Equal(4, queue.PeekRef());
        }

        [Fact]
        public void PeekRef_Empty()
        {
            ImmutableQueue<int> queue = ImmutableQueue<int>.Empty;

            Assert.Throws<InvalidOperationException>(() => queue.PeekRef());
        }

        protected override IEnumerable<T> GetEnumerableOf<T>(params T[] contents)
        {
            ImmutableQueue<T> queue = ImmutableQueue<T>.Empty;
            foreach (T item in contents)
            {
                queue = queue.Enqueue(item);
            }

            return queue;
        }
    }
}
