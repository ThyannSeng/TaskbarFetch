// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

#nullable disable

using System;

namespace TaskbarFetch
{
    /// <summary>
    /// Stores one pending click and provides isolated snapshots for asynchronous evaluation.
    /// </summary>
    internal sealed class PendingClickStore<T> where T : class
    {
        private readonly object _sync = new object();
        private readonly Func<T, T> _snapshot;
        private int _pendingId;
        private T _pendingValue;

        public PendingClickStore(Func<T, T> snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            _snapshot = snapshot;
        }

        public void Replace(int pendingId, T value)
        {
            if (pendingId == 0)
                throw new ArgumentOutOfRangeException(nameof(pendingId));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            lock (_sync)
            {
                _pendingId = pendingId;
                _pendingValue = value;
            }
        }

        public bool TryGetSnapshot(out int pendingId, out T snapshot)
        {
            lock (_sync)
            {
                if (_pendingValue == null)
                {
                    pendingId = 0;
                    snapshot = null;
                    return false;
                }

                pendingId = _pendingId;
                snapshot = _snapshot(_pendingValue);
                return true;
            }
        }

        public bool IsCurrent(int pendingId)
        {
            lock (_sync)
            {
                return _pendingValue != null && _pendingId == pendingId;
            }
        }

        public bool TryUpdate(int pendingId, Action<T> update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            lock (_sync)
            {
                if (_pendingValue == null || _pendingId != pendingId)
                    return false;

                update(_pendingValue);
                return true;
            }
        }

        public bool TryTake(int pendingId, out T value)
        {
            lock (_sync)
            {
                if (_pendingValue == null || _pendingId != pendingId)
                {
                    value = null;
                    return false;
                }

                value = _pendingValue;
                _pendingValue = null;
                _pendingId = 0;
                return true;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _pendingValue = null;
                _pendingId = 0;
            }
        }
    }
}
