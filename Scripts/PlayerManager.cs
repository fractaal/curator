using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// PlayerManager serves as the single source of truth for all player-related information
/// in both single-player and multiplayer scenarios.
/// </summary>
public partial class PlayerManager : Node
{
	private static PlayerManager instance;
	private Dictionary<int, Node3D> players = new Dictionary<int, Node3D>();
	private Dictionary<int, PlayerStats> playerStats = new Dictionary<int, PlayerStats>();

	public override void _Ready()
	{
		base._Ready();
		instance = this;

		// Connect to multiplayer events
		var multiplayerManager = GetNode<Node>("/root/MultiplayerManager");
		if (multiplayerManager != null)
		{
			GD.Print("PlayerManager: Connected to MultiplayerManager");
			// MultiplayerManager will call RegisterPlayer/UnregisterPlayer directly
		}
		else
		{
			GD.PrintErr("PlayerManager: MultiplayerManager not found - this is required for the new architecture");
		}
	}

	public static PlayerManager Get()
	{
		if (instance == null)
		{
			throw new System.Exception("PlayerManager should be an autoload");
		}
		return instance;
	}

	/// <summary>
	/// Register a player with the manager
	/// </summary>
	public void RegisterPlayer(int playerId, Node3D playerNode)
	{
		if (playerNode == null)
		{
			GD.PrintErr($"PlayerManager: Attempted to register null player with ID {playerId}");
			return;
		}

		players[playerId] = playerNode;
		
		// Try to get PlayerStats from the player node
		var stats = playerNode.GetNode<PlayerStats>("PlayerStats");
		if (stats != null)
		{
			playerStats[playerId] = stats;
			GD.Print($"PlayerManager: Registered player {playerId} with stats");
		}
		else
		{
			GD.PrintErr($"PlayerManager: Player {playerId} has no PlayerStats node");
		}
	}

	/// <summary>
	/// Unregister a player from the manager
	/// </summary>
	public void UnregisterPlayer(int playerId)
	{
		if (players.ContainsKey(playerId))
		{
			players.Remove(playerId);
			playerStats.Remove(playerId);
			GD.Print($"PlayerManager: Unregistered player {playerId}");
		}
	}

	/// <summary>
	/// Get all currently registered players
	/// </summary>
	public Dictionary<int, Node3D> GetAllPlayers()
	{
		return new Dictionary<int, Node3D>(players);
	}

	/// <summary>
	/// Get all currently registered player stats
	/// </summary>
	public Dictionary<int, PlayerStats> GetAllPlayerStats()
	{
		return new Dictionary<int, PlayerStats>(playerStats);
	}

	/// <summary>
	/// Get a specific player by ID
	/// </summary>
	public Node3D GetPlayer(int playerId)
	{
		return players.TryGetValue(playerId, out var player) ? player : null;
	}

	/// <summary>
	/// Get a specific player's stats by ID
	/// </summary>
	public PlayerStats GetPlayerStats(int playerId)
	{
		return playerStats.TryGetValue(playerId, out var stats) ? stats : null;
	}

	/// <summary>
	/// Get the first player (useful for single-player scenarios or getting "a" player)
	/// </summary>
	public Node3D GetFirstPlayer()
	{
		return players.Values.FirstOrDefault();
	}

	/// <summary>
	/// Get the first player's stats (useful for single-player scenarios)
	/// </summary>
	public PlayerStats GetFirstPlayerStats()
	{
		return playerStats.Values.FirstOrDefault();
	}

	/// <summary>
	/// Get the number of registered players
	/// </summary>
	public int GetPlayerCount()
	{
		return players.Count;
	}

	/// <summary>
	/// Check if a player is registered
	/// </summary>
	public bool HasPlayer(int playerId)
	{
		return players.ContainsKey(playerId);
	}


}
