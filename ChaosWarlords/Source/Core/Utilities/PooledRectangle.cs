using Microsoft.Xna.Framework;
using System;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Pooled wrapper for Rectangle struct to avoid boxing allocations.
    /// Use with 'using' statement for automatic return to pool.
    /// </summary>
    /// <remarks>
    /// This class follows SRP by only managing Rectangle pooling.
    /// The pool instance is managed by PoolManager for multiplayer context isolation.
    /// </remarks>
    /// <example>
    /// using var rect = PooledRectangle.Rent(10, 20, 100, 50);
    /// spriteBatch.Draw(texture, rect.Value, Color.White);
    /// // Automatically returned to pool when scope exits
    /// </example>
    public sealed class PooledRectangle : IDisposable
    {
        // Static pool for single-player/client rendering
        // In multiplayer, use PoolManager.GetRectanglePool() for context-specific pools
        private static readonly ObjectPool<PooledRectangle> _defaultPool = new ObjectPool<PooledRectangle>(64, 256);

        /// <summary>
        /// The wrapped Rectangle value.
        /// </summary>
        public Rectangle Value { get; set; }

        private bool _disposed;
        private ObjectPool<PooledRectangle>? _sourcePool;

        /// <summary>
        /// Rents a pooled Rectangle with specified dimensions from the default pool.
        /// </summary>
        public static PooledRectangle Rent(int x, int y, int width, int height)
        {
            return Rent(x, y, width, height, _defaultPool);
        }

        /// <summary>
        /// Rents a pooled Rectangle from an existing Rectangle using the default pool.
        /// </summary>
        public static PooledRectangle Rent(Rectangle rect)
        {
            return Rent(rect.X, rect.Y, rect.Width, rect.Height, _defaultPool);
        }

        /// <summary>
        /// Rents a pooled Rectangle from a specific pool (for multiplayer context isolation).
        /// </summary>
        internal static PooledRectangle Rent(int x, int y, int width, int height, ObjectPool<PooledRectangle> pool)
        {
            var pooled = pool.Rent();
            pooled.Value = new Rectangle(x, y, width, height);
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
        /// Implicit conversion to Rectangle for convenience.
        /// </summary>
        public static implicit operator Rectangle(PooledRectangle pooled) => pooled.Value;
    }
}
