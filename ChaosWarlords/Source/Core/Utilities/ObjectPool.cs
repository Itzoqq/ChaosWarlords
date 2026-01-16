using System;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Generic object pool to reduce GC pressure from frequent allocations.
    /// Thread-safe implementation for reusing objects instead of creating new instances.
    /// </summary>
    /// <typeparam name="T">Type of object to pool. Must be a reference type with parameterless constructor.</typeparam>
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Stack<T> _available;
        private readonly int _maxSize;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new object pool with specified capacity.
        /// </summary>
        /// <param name="initialCapacity">Number of objects to pre-allocate.</param>
        /// <param name="maxSize">Maximum number of objects to keep in pool.</param>
        public ObjectPool(int initialCapacity = 32, int maxSize = 128)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Initial capacity must be non-negative.");
            if (maxSize < initialCapacity)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be >= initial capacity.");

            _available = new Stack<T>(initialCapacity);
            _maxSize = maxSize;

            // Pre-populate pool to avoid allocations during gameplay
            for (int i = 0; i < initialCapacity; i++)
            {
                _available.Push(new T());
            }
        }

        /// <summary>
        /// Rents an object from the pool. Creates a new instance if pool is empty.
        /// </summary>
        /// <returns>An object instance ready for use.</returns>
        public T Rent()
        {
            lock (_lock)
            {
                return _available.Count > 0 ? _available.Pop() : new T();
            }
        }

        /// <summary>
        /// Returns an object to the pool for reuse.
        /// </summary>
        /// <param name="obj">Object to return. Null values are ignored.</param>
        public void Return(T obj)
        {
            if (obj == null) return;

            lock (_lock)
            {
                if (_available.Count < _maxSize)
                {
                    _available.Push(obj);
                }
                // If pool is full, let object be garbage collected
            }
        }

        /// <summary>
        /// Clears all pooled objects. Use sparingly (e.g., scene transitions).
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _available.Clear();
            }
        }

        /// <summary>
        /// Gets the current number of available objects in the pool.
        /// </summary>
        public int AvailableCount
        {
            get
            {
                lock (_lock)
                {
                    return _available.Count;
                }
            }
        }
    }
}
