using ChaosWarlords.Source.Utilities;
using System;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for logging mechanisms. 
    /// Allows swapping between Console, File, or Buffered/Async logging strategies.
    /// </summary>
    public interface IGameLogger
    {
        /// <summary>
        /// Logs a string message to the specified channel.
        /// </summary>
        void Log(string message, LogChannel channel = LogChannel.General);

        /// <summary>
        /// Logs an object (using .ToString()) to the specified channel.
        /// </summary>
        void Log(object message, LogChannel channel = LogChannel.General);
    }
}
