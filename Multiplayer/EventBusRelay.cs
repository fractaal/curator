using Godot;

/// <summary>
/// The ONLY place EventBus signals cross the network. EventBus itself stays
/// process-local (it carries LLM-internal chatter that must never leak to
/// clients); this autoload relays an enumerated set of crossings:
///
///   Inbound  (client -> host):  PlayerTalked, NotableEventOccurred,
///                               PlayerDecidedGhostType
///   Outbound (host -> clients): PlayerEffect, GameWon, GameLost,
///                               EndgameSummary, GhostBackstory
///
/// Loop safety is structural: the inbound and outbound sets are disjoint,
/// and each direction gates on IsServer(). A host re-emission of an inbound
/// signal hits an inbound handler that no-ops on the server; a client
/// re-emission of an outbound signal hits an outbound handler that no-ops
/// off the server.
/// </summary>
public partial class EventBusRelay : Node
{
	private EventBus Bus;

	public override void _Ready()
	{
		Bus = EventBus.Get();

		// ---- Inbound: client emissions forwarded to the host bus (the brain's senses)
		Bus.PlayerTalked += message => ForwardToHost(MethodName.HostPlayerTalked, message);
		Bus.NotableEventOccurred += message =>
			ForwardToHost(MethodName.HostNotableEventOccurred, message);
		Bus.PlayerDecidedGhostType += message =>
			ForwardToHost(MethodName.HostPlayerDecidedGhostType, message);

		// ---- Outbound: host emissions broadcast so each client's bus fires locally
		Bus.PlayerEffect += (verb, arguments, targetPeerId) =>
			BroadcastToClients(MethodName.ClientPlayerEffect, verb, arguments, targetPeerId);
		Bus.GameWon += message => BroadcastToClients(MethodName.ClientGameWon, message);
		Bus.GameLost += message => BroadcastToClients(MethodName.ClientGameLost, message);
		Bus.EndgameSummary += message =>
			BroadcastToClients(MethodName.ClientEndgameSummary, message);
		Bus.GhostBackstory += message =>
			BroadcastToClients(MethodName.ClientGhostBackstory, message);
	}

	private void ForwardToHost(StringName method, params Variant[] args)
	{
		// Host emissions already reach the brain; only clients forward.
		if (Multiplayer.IsServer())
		{
			return;
		}
		RpcId(1, method, args);
	}

	private void BroadcastToClients(StringName method, params Variant[] args)
	{
		// Only the host broadcasts, and only when peers exist (solo = no-op).
		if (!Multiplayer.IsServer() || Multiplayer.GetPeers().Length == 0)
		{
			return;
		}
		// No CallLocal: the host bus already fired this signal.
		Rpc(method, args);
	}

	// ---- Inbound receivers (run on host) ------------------------------------

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void HostPlayerTalked(string message)
	{
		Bus.EmitSignal(EventBus.SignalName.PlayerTalked, message);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void HostNotableEventOccurred(string message)
	{
		Bus.EmitSignal(EventBus.SignalName.NotableEventOccurred, message);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void HostPlayerDecidedGhostType(string message)
	{
		Bus.EmitSignal(EventBus.SignalName.PlayerDecidedGhostType, message);
	}

	// ---- Outbound receivers (run on clients) --------------------------------

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void ClientPlayerEffect(string verb, string arguments, long targetPeerId)
	{
		Bus.EmitSignal(EventBus.SignalName.PlayerEffect, verb, arguments, targetPeerId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void ClientGameWon(string message)
	{
		Bus.EmitSignal(EventBus.SignalName.GameWon, message);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void ClientGameLost(string message)
	{
		Bus.EmitSignal(EventBus.SignalName.GameLost, message);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void ClientEndgameSummary(string message)
	{
		Bus.EmitSignal(EventBus.SignalName.EndgameSummary, message);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void ClientGhostBackstory(string message)
	{
		Bus.EmitSignal(EventBus.SignalName.GhostBackstory, message);
	}
}
