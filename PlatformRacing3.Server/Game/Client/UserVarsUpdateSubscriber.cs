using Microsoft.Extensions.Logging;
using PlatformRacing3.Common.Redis;
using PlatformRacing3.Common.Utils;
using PlatformRacing3.Server.Core;
using PlatformRacing3.Server.Game.Communication.Messages.Outgoing;
using StackExchange.Redis;
using System.Text.Json;
using System.Collections.Generic;

namespace PlatformRacing3.Server.Game.Client;

/// <summary>
/// Listens for Redis notifications about user variable updates from the Web project
/// and forwards them to the client sessions.
/// </summary>
internal class UserVarsUpdateSubscriber : IDisposable
{
    private readonly ILogger<UserVarsUpdateSubscriber> _logger;
    private readonly ClientManager _clientManager;
    private ISubscriber _subscriber;
    private bool _isInitialized = false;

    /// <summary>
    /// Data structure for deserializing the user vars update message from Redis
    /// </summary>
    private class UserVarsUpdateData
    {
        public uint UserId { get; set; }
        public uint Rank { get; set; }
        public ulong Exp { get; set; }
        public ulong TotalExpGain { get; set; }
        public object[][] ExpArray { get; set; }
        // We'll ignore campaign data even if it's in the message
    }

    public UserVarsUpdateSubscriber(ILogger<UserVarsUpdateSubscriber> logger, ClientManager clientManager)
    {
        _logger = logger;
        _clientManager = clientManager;
        
        // Don't try to connect to Redis immediately
        // We'll initialize when explicitly called
        _logger.LogInformation("UserVarsUpdateSubscriber created - waiting for Redis initialization");
    }
    
    /// <summary>
    /// Initialize and subscribe to Redis channel when the server is ready.
    /// This should be called after Redis is initialized.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        
        try
        {
            var redis = RedisConnection.GetConnectionMultiplexer();
            if (redis == null || !redis.IsConnected)
            {
                _logger.LogWarning("Redis connection not available - UserVarsUpdateSubscriber initialization delayed");
                return;
            }
            
            _subscriber = redis.GetSubscriber();
            
            // Subscribe to the UserVarsUpdate channel
            _subscriber.Subscribe(RedisChannel.Literal("UserVarsUpdate"), HandleUserVarsUpdate, CommandFlags.FireAndForget);
            
            _isInitialized = true;
            _logger.LogInformation("UserVarsUpdateSubscriber initialized and listening for updates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize UserVarsUpdateSubscriber");
        }
    }

    /// <summary>
    /// Handles user variable updates from Redis
    /// </summary>
    private void HandleUserVarsUpdate(RedisChannel channel, RedisValue message)
    {
        try
        {
            _logger.LogDebug($"Received message on {channel}");
            
            // Parse the message
            var data = JsonSerializer.Deserialize<UserVarsUpdateData>(message.ToString());
            if (data == null)
            {
                _logger.LogWarning("Received null user vars update data from Redis");
                return;
            }

            _logger.LogDebug($"Processing XP update for user ID {data.UserId}, XP: {data.Exp}, Gain: {data.TotalExpGain}");

            // Find the client session for this user
            if (_clientManager.TryGetClientSessionByUserId(data.UserId, out var clientSession))
            {
                _logger.LogDebug($"Found client session for user {data.UserId}");
                
                try
                {
                    // First, update the user's data in memory to ensure consistency
                    if (clientSession.UserData != null)
                    {
                        // Use reflection to update the user's exp and rank if setters aren't accessible
                        try
                        {
                            // Try to use the property directly first
                            if (clientSession.UserData.GetType().GetProperty("Exp")?.SetMethod != null)
                            {
                                clientSession.UserData.GetType().GetProperty("Exp").SetValue(clientSession.UserData, data.Exp);
                            }
                            
                            if (clientSession.UserData.GetType().GetProperty("Rank")?.SetMethod != null)
                            {
                                clientSession.UserData.GetType().GetProperty("Rank").SetValue(clientSession.UserData, data.Rank);
                            }
                        }
                        catch (Exception propEx)
                        {
                            _logger.LogError(propEx, "Error updating user data properties");
                        }
                    }
                    
                    // Calculate the next rank's XP requirement
                    ulong maxExp = ExpUtils.GetNextRankExpRequirement(data.Rank);
                    
                    // Send the YouFinished message with the XP information
                    clientSession.SendPacket(new YouFinishedOutgoingMessage(
                        data.Rank,                // Rank
                        data.Exp,                 // Current Exp
                        maxExp,                   // Max Exp for this rank
                        data.TotalExpGain,        // Total XP gained
                        data.ExpArray,            // Explanation of XP gain
                        1                         // Place (always 1 in single player)
                    ));
                    
                    // Only update the XP and rank variables - we don't need to send campaign data
                    var userVars = new Dictionary<string, object>
                    {
                        ["exp"] = data.Exp,
                        ["rank"] = data.Rank
                    };
                    
                    // Send the UserVars update to the client
                    clientSession.SendPacket(new UserVarsOutgoingMessage(clientSession.SocketId, userVars));
                    
                    _logger.LogDebug($"Successfully sent XP update to user {clientSession.UserData?.Username}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending update packets to user");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user vars update from Redis");
        }
    }

    public void Dispose()
    {
        if (_subscriber != null && _isInitialized)
        {
            try
            {
                _subscriber.Unsubscribe(RedisChannel.Literal("UserVarsUpdate"), HandleUserVarsUpdate, CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from Redis");
            }
        }
    }
} 