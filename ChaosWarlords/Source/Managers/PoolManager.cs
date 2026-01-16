using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Core.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Manages object pools for different contexts (client/server/replay).
    /// Follows SRP by only managing pool lifecycle and context isolation.
    /// </summary>
    /// <remarks>
    /// In multiplayer scenarios, you may need separate pools for:
    /// - Client rendering (local player's view)
    /// - Server simulation (authoritative state)
    /// - Replay playback (historical state)
    /// 
    /// This manager allows creating and disposing context-specific pools
    /// to prevent cross-contamination between different game contexts.
    /// </remarks>
    public sealed class PoolManager : IDisposable
    {
        private readonly Dictionary<string, object> _pools = new Dictionary<string, object>();
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// Gets or creates a pool for the specified type and context.
        /// </summary>
        /// <typeparam name="T">Type of object to pool.</typeparam>
        /// <param name="contextKey">Unique key for this context (e.g., "client", "server", "replay").</param>
        /// <param name="initialCapacity">Initial pool capacity.</param>
        /// <param name="maxSize">Maximum pool size.</param>
        public ObjectPool<T> GetOrCreatePool<T>(string contextKey, int initialCapacity = 32, int maxSize = 128)
            where T : class, new()
        {
            if (string.IsNullOrEmpty(contextKey))
                throw new ArgumentException("Context key cannot be null or empty.", nameof(contextKey));

            lock (_lock)
            {
                var poolKey = $"{typeof(T).FullName}_{contextKey}";

                if (_pools.TryGetValue(poolKey, out var existingPool))
                {
                    return (ObjectPool<T>)existingPool;
                }

                var newPool = new ObjectPool<T>(initialCapacity, maxSize);
                _pools[poolKey] = newPool;
                return newPool;
            }
        }

        /// <summary>
        /// Clears all pools for a specific context (e.g., when disconnecting from server).
        /// </summary>
        public void ClearContext(string contextKey)
        {
            if (string.IsNullOrEmpty(contextKey))
                throw new ArgumentException("Context key cannot be null or empty.", nameof(contextKey));

            lock (_lock)
            {
                var keysToRemove = new List<string>();

                foreach (var key in _pools.Keys)
                {
                    if (key.EndsWith($"_{contextKey}", StringComparison.Ordinal))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    if (_pools[key] is IDisposable disposable)
                    {
                        // ObjectPool doesn't implement IDisposable currently, but this is future-proof
                        disposable.Dispose();
                    }
                    _pools.Remove(key);
                }
            }
        }

        /// <summary>
        /// Clears all pools across all contexts.
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                foreach (var pool in _pools.Values)
                {
                    if (pool is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                _pools.Clear();
            }
        }

        /// <summary>
        /// Disposes all pools and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                ClearAll();
                _disposed = true;
            }
        }

        /// <summary>
        /// Gets the number of active pool contexts.
        /// </summary>
        public int ActiveContextCount
        {
            get
            {
                lock (_lock)
                {
                    return _pools.Count;
                }
            }
        }
    }
}
