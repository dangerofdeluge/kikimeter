using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace KikiMeter
{
    public class CircularBuffer<T>
    {
        private readonly ReaderWriterLockSlim slimLock = new ReaderWriterLockSlim();
        private readonly int capacity;
        private readonly T[] buffer;
        private readonly int upperBound;

        private int currentIndex = -1;
        private int currentStart = 0;

        public CircularBuffer(int size)
        {
            if (size < 1)
            {
                throw new ArgumentException($"{nameof(size)} cannot be negative nor zero");
            }

            buffer = new T[size];
            capacity = size;
            upperBound = size - 1;
        }

        public void Add(T value)
        {
            slimLock.EnterWriteLock();
            try
            {

                if (IsFull || currentIndex == upperBound)
                {
                    currentStart = FetchNextSlot(currentStart);
                    IsFull = true;
                }

                currentIndex = FetchNextSlot(currentIndex);
                buffer[currentIndex] = value;

            }
            finally
            {
                slimLock.ExitWriteLock();
            }

        }

        public bool IsFull { get; private set; }

        private int FetchNextSlot(int value)
        {
            return (value + 1) % capacity;
        }

        public IEnumerable<T> Latest()
        {
            slimLock.EnterReadLock();
            try
            {
                return FetchItems().ToArray();
            }
            finally
            {
                slimLock.ExitReadLock();
            }
        }

        private IEnumerable<T> FetchItems()
        {
            IEnumerable<T> fetchedItems = Enumerable.Empty<T>();
            if (IsFull)
            {
                 fetchedItems = fetchedItems.Concat(FetchItems(currentStart, upperBound));
            }
            return fetchedItems.Concat(FetchItems(0, currentIndex));
        }
        private IEnumerable<T> FetchItems(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                yield return buffer[i];
            }
        }
    }
}
