using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace FilterWheelShared.Common
{
    public class BlockingSkipFrameCollection<T>
    {
        private T _currentV;
        public T CurrentValue
        {
            get { return _currentV; }
            set { _currentV = value; }
        }

        private readonly ConcurrentQueue<T> _data;
        public BlockingSkipFrameCollection()
        {
            _capacity = 2;
            _data = new ConcurrentQueue<T>();
        }

        private readonly int _capacity;

        public int Capacity
        {
            get { return _capacity; }
        }

        public bool IsAddingComplete { get; private set; } = false;

        public BlockingSkipFrameCollection(int capacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentException("Capacity must not be less than 2.");
            }
            _capacity = capacity;
            _data = new ConcurrentQueue<T>();
        }

        private readonly AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        public void Add(T t)
        {
            if (IsAddingComplete)
                throw new InvalidOperationException("Can not add item when IsAddingCompleted is true.");
            _data.Enqueue(t);
            autoResetEvent.Set();
            while (_data.Count > _capacity)
            {
                if (_data.TryDequeue(out T tt) && (_currentV == null || !_currentV.Equals(tt)))
                    DiscardData?.Invoke(this, tt);
            }
        }

        public T Take()
        {
            T t = default(T);
            while (_data.TryDequeue(out t) == false)
                autoResetEvent.WaitOne();
            return t;
        }

        public bool TryTake(out T t)
        {
            return _data.TryDequeue(out t);
        }

        public IEnumerable<T> GetConsumingEnumerable()
        {
            while (IsAddingComplete == false)
                yield return Take();
        }

        public void CompleteAdding()
        {
            IsAddingComplete = true;
        }

        public event EventHandler<T> DiscardData;
    }

}
