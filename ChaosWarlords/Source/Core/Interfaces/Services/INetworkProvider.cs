using ChaosWarlords.Source.Core.Data.Dtos;
using System;
using System.Threading.Tasks;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Abstraction for the network layer.
    /// Allows the game to run in Local P2P, Dedicated Server, or Singleplayer (Loopback) modes.
    /// </summary>
    public interface INetworkProvider
    {
        /// <summary>
        /// Sends a command to the authority (Server or Logic Host).
        /// </summary>
        Task SendCommandAsync(GameCommandDto command);

        /// <summary>
        /// Callback triggered when a command is received from the network (or loopback).
        /// </summary>
        event Action<GameCommandDto> OnCommandReceived;

        /// <summary>
        /// Callback triggered when a full game state snapshot is received (reconnection/sync).
        /// </summary>
        event Action<GameStateDto> OnStateReceived;

        /// <summary>
        /// Connects to the specified endpoint.
        /// </summary>
        Task ConnectAsync(string endpoint);

        /// <summary>
        /// Disconnects from the current session.
        /// </summary>
        Task DisconnectAsync();

        bool IsConnected { get; }
    }
}
