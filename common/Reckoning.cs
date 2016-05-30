using System;
using System.Collections.Generic;

namespace Kfp
{
    public class Reckoning<T, O> where T : struct
    {
        private T _head;
        private readonly SortedList<ulong, T> _history;
        private readonly Dictionary<O, ulong?> _positions;

        public Reckoning() : this(default(T)) { }

        public Reckoning(T head)
        {
            _head = head;
            _history = new SortedList<ulong, T>();
            _positions = new Dictionary<O, ulong?>();
        }

        public T Head {
            get { return _head; }
        }

        public void AddMoment(ulong position, Diff<T> diff) {
            if (_positions.Count == 0) {
                _history.Clear();
            }

            diff.Apply(ref _head);
            _history.Add(position, _head);
        }

        public void AddMoment(ulong position, T item) {
            if (_positions.Count == 0) {
                _history.Clear();
            }

            _head = item;
            _history.Add(position, item);
        }

        public void AddObserver(O observer) {
            _positions.Add(observer, null);
        }

        public bool RemoveObserver(O observer) {
            if (_positions.Remove(observer)) {
                Prune();
                return true;
            }
            return false;
        }

        public void NotifyObserverPosition(O observer, ulong position) {
            if (!_positions.ContainsKey(observer)) {
                throw new InvalidOperationException("no such observer");
            }

            _positions[observer] = position;
            Prune();
        }

        public Diff<T> GetDiff(O observer) {
            ulong? position = _positions[observer];

            if (!position.HasValue) {
                return Diff.Create(null, _head);
            }

            var old = _history[position.Value];

            return Diff.Create(old, _head);
        }

        private void Prune() {
            var oldSize = _history.Count;

            var toRemove = new HashSet<ulong>(_history.Keys);
            foreach (var entry in _positions) {
                if (entry.Value.HasValue) {
                    toRemove.Remove(entry.Value.Value);
                }
            }

            foreach (var key in toRemove) {
                _history.Remove(key);
            }
        }
    }
}
