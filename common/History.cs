using System;
using System.Collections.Generic;

namespace Kfp
{
    public class History<T>
    {
        private int? _capacity;
        private int _revision;
        private readonly BoundedQueue<T> _buffer;

        public History(int capacity) {
            if (capacity < 0) {
                throw new ArgumentOutOfRangeException("capacity");
            }

            _capacity = capacity;
            _revision = 0;
            _buffer = new BoundedQueue<T>(capacity);
        }

        public T Current {
            get { return _buffer[0]; }
        }

        public int Revision {
            get { return _revision; }
        }

        public bool IsEmpty {
            get { return _buffer.Count == 0; }
        }

        public int Count {
            get { return _buffer.Count; }
        }

        public int Add(T item) {
            _revision++;
            if (_capacity.HasValue) {
                if (_capacity.Value == 0) {
                    return _revision;
                } else if (_capacity.Value == _buffer.Count) {
                    _buffer.Dequeue();
                }
            }

            _buffer.Enqueue(item);
            return _revision;
        }

        public void RemoveTo(int revision) {
            int count = _revision - revision;
            if (count > _buffer.Count) {
                throw new ArgumentOutOfRangeException("revision");
            }
            _buffer.TrimHead(count);
        }

        public bool TryGetRevision(int revision, out T item) {
            int index = _revision - revision;
            if (index >= _buffer.Count) {
                item = default(T);
                return false;
            }

            item = _buffer[index];
            return true;
        }

        public void Clear() {
            _revision = 0;
            _buffer.Clear();
        }
    }
}
