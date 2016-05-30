using System;

namespace Kfp
{
    public class BoundedQueue<T>
    {
        private readonly T[] _items;
        private int _start;
        private int _count;

        public BoundedQueue(int capacity) {
            _items = new T[capacity];
            _start = 0;
            _count = 0;
        }

        public void Enqueue(T item) {
            _items[TranslateIndex(_count)] = item;
            if (_count < _items.Length) {
                _count++;
            } else {
                SetStart(_start + 1);
            }
        }

        public T Dequeue() {
            if (_count == 0) {
                throw new InvalidOperationException("queue is empty");
            }

            var item = _items[_start];
            _items[_start] = default(T);
            SetStart(_start + 1);
            _count--;
            return item;
        }

        public void Clear() {
            Array.Clear(_items, 0, _items.Length);
            _start = _count = 0;
        }

        public void TrimHead(int count) {
            if (count > _count) {
                throw new ArgumentOutOfRangeException("count");
            }
            SetStart(_start + count);
            _count -= count;
        }

        public int Count { get { return _count; } }

        public T this[int index] {
            get {
                if (index < 0 || index >= _count) {
                    throw new ArgumentOutOfRangeException("index");
                }
                return _items[TranslateIndex(index)];
            }
        }

        private void SetStart(int start) {
            _start = start % _items.Length;
        }

        private int TranslateIndex(int index) {
            return (_start + index) % _items.Length;
        }
    }
}
