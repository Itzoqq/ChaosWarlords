using Microsoft.Xna.Framework;
using System;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Pooled wrapper for Vector2 struct to avoid boxing allocations.
    /// Use with 'using' statement for automatic return to pool.
    /// </summary>
    /// <remarks>
    /// This class follows SRP by only managing Vector2 pooling.
    /// The pool instance is managed by PoolManager for multiplayer context isolation.
    /// </remarks>
    /// <example>
    /// using var pos = PooledVector2.Rent(100f, 200f);
    /// spriteBatch.DrawString(font, "Text", pos.Value, Color.White);
    /// // Automatically returned to pool when scope exits
    /// </example>
    public sealed class PooledVector2 : IDisposable
    {
        // Static pool for single-player/client rendering
        // In multiplayer, use PoolManager.GetVector2Pool() for context-specific pools
        private static readonly ObjectPool<PooledVector2> _defaultPool = new ObjectPool<PooledVector2>(64, 256);

        /// <summary>
        /// The wrapped Vector2 value.
        /// </summary>
        public Vector2 Value { get; set; }

        private bool _disposed;
        private ObjectPool<PooledVector2>? _sourcePool;

        /// <summary>
        /// Rents a pooled Vector2 with specified coordinates from the default pool.
        /// </summary>
        public static PooledVector2 Rent(float x, float y)
        {
            return Rent(x, y, _defaultPool);
        }

        /// <summary>
        /// Rents a pooled Vector2 from an existing Vector2 using the default pool.
        /// </summary>
        public static PooledVector2 Rent(Vector2 vector)
        {
            return Rent(vector.X, vector.Y, _defaultPool);
        }

        /// <summary>
        /// Rents a pooled Vector2 from a specific pool (for multiplayer context isolation).
        /// </summary>
        internal static PooledVector2 Rent(float x, float y, ObjectPool<PooledVector2> pool)
        {
            var pooled = pool.Rent();
            pooled.Value = new Vector2(x, y);
            pooled._disposed = false;
            pooled._sourcePool = pool;
            return pooled;
        }

        /// <summary>
        /// Returns this instance to the pool. Called automatically by 'using' statement.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                var pool = _sourcePool ?? _defaultPool;
                pool.Return(this);
                _disposed = true;
                _sourcePool = null;
            }
        }

        /// <summary>
        /// Implicit conversion to Vector2 for convenience.
        /// </summary>
        public static implicit operator Vector2(PooledVector2 pooled) => pooled.Value;
    }
}
