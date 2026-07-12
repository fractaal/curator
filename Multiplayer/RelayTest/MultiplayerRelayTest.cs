using System.Linq;
using Godot;

/// <summary>
/// Two-instance ENet loopback proof of the multiplayer crossings, no game scene needed.
/// Verifies EventBusRelay in both directions over a real network peer, plus LanDiscovery
/// beacon → lobby-list. Run with:
///
///   Multiplayer/RelayTest/run.sh
///
/// (host instance: `godot --headless <scene> -- --host`, client: `-- --client`;
/// each exits 0 on PASS, 1 on FAIL, and run.sh requires both.)
///
/// Host asserts it received, via the relay, the client's PlayerTalked /
/// NotableEventOccurred / PlayerDecidedGhostType — and a final PlayerTalked echo the
/// client only sends after IT received the host's PlayerEffect + GameLost broadcasts.
/// The client additionally asserts the host's discovery beacon produced a lobby entry.
/// </summary>
public partial class MultiplayerRelayTest : Node
{
	private const int Port = 4599;
	private const double TimeoutSeconds = 25;

	private bool isHost;
	private double elapsed;
	private bool concluded;

	// Host-side assertions
	private bool sawTalk;
	private bool sawNotable;
	private bool sawGuess;
	private bool sawOutboundEcho;

	// Client-side assertions/state
	private bool gotEffect;
	private bool gotGameLost;
	private bool sentEcho;
	private bool sawLobby;

	private EventBus bus;
	private LanDiscovery discovery;

	public override void _Ready()
	{
		bus = EventBus.Get();
		discovery = GetNode<LanDiscovery>("/root/LanDiscovery");
		isHost = OS.GetCmdlineUserArgs().Contains("--host");

		var peer = new ENetMultiplayerPeer();

		if (isHost)
		{
			var err = peer.CreateServer(Port, 4);
			if (err != Error.Ok)
			{
				GD.PrintErr($"[RelayTest] host: CreateServer failed ({err})");
				GetTree().Quit(1);
				return;
			}
			Multiplayer.MultiplayerPeer = peer;

			discovery.StartBeacon(4);

			bus.PlayerTalked += message =>
			{
				if (message.Contains("hello-from-client"))
				{
					sawTalk = true;
				}
				if (message.Contains("outbound-ok"))
				{
					sawOutboundEcho = true;
				}
			};
			bus.NotableEventOccurred += message =>
			{
				if (message.Contains("client-notable"))
				{
					sawNotable = true;
				}
			};
			bus.PlayerDecidedGhostType += message =>
			{
				if (message.Contains("Wraith"))
				{
					sawGuess = true;
				}
			};

			Multiplayer.PeerConnected += id =>
			{
				GD.Print($"[RelayTest] host: peer {id} connected, firing outbound crossings");
				// Give the client a beat to finish _Ready, then fire what the relay
				// must broadcast: a targeted player effect and an endgame signal.
				GetTree().CreateTimer(1.0).Timeout += () =>
				{
					bus.EmitSignal(
						EventBus.SignalName.PlayerEffect,
						"throwplayeraround",
						"",
						42L
					);
					bus.EmitSignal(EventBus.SignalName.GameLost, "relay-test loss");
				};
			};

			GD.Print("[RelayTest] host: listening on port ", Port);
		}
		else
		{
			var err = peer.CreateClient("127.0.0.1", Port);
			if (err != Error.Ok)
			{
				GD.PrintErr($"[RelayTest] client: CreateClient failed ({err})");
				GetTree().Quit(1);
				return;
			}
			Multiplayer.MultiplayerPeer = peer;

			discovery.StartDiscovery();

			bus.PlayerEffect += (verb, arguments, targetPeerId) =>
			{
				if (verb == "throwplayeraround" && targetPeerId == 42)
				{
					gotEffect = true;
				}
			};
			bus.GameLost += message =>
			{
				if (message.Contains("relay-test"))
				{
					gotGameLost = true;
				}
			};

			Multiplayer.ConnectedToServer += () =>
			{
				GD.Print("[RelayTest] client: connected, firing inbound crossings");
				bus.EmitSignal(EventBus.SignalName.PlayerTalked, "hello-from-client");
				bus.EmitSignal(EventBus.SignalName.NotableEventOccurred, "client-notable");
				bus.EmitSignal(EventBus.SignalName.PlayerDecidedGhostType, "Wraith");
			};
		}
	}

	public override void _Process(double delta)
	{
		if (concluded)
		{
			return;
		}

		elapsed += delta;

		if (isHost)
		{
			if (sawTalk && sawNotable && sawGuess && sawOutboundEcho)
			{
				Conclude(true);
			}
			else if (elapsed > TimeoutSeconds)
			{
				Conclude(false);
			}
			return;
		}

		if (!sawLobby && discovery.GetLobbies().Count > 0)
		{
			GD.Print("[RelayTest] client: lobby discovered");
			sawLobby = true;
		}

		if (gotEffect && gotGameLost && !sentEcho)
		{
			sentEcho = true;
			bus.EmitSignal(EventBus.SignalName.PlayerTalked, "outbound-ok");
			// Let the echo RPC flush before concluding.
			GetTree().CreateTimer(2.0).Timeout += () => Conclude(sawLobby);
		}
		else if (elapsed > TimeoutSeconds)
		{
			Conclude(false);
		}
	}

	private void Conclude(bool pass)
	{
		concluded = true;

		var checks = isHost
			? new (string, bool)[]
			{
				("inbound PlayerTalked relayed to host", sawTalk),
				("inbound NotableEventOccurred relayed to host", sawNotable),
				("inbound PlayerDecidedGhostType relayed to host", sawGuess),
				("client echoed after receiving outbound PlayerEffect+GameLost", sawOutboundEcho),
			}
			: new (string, bool)[]
			{
				("outbound targeted PlayerEffect reached client", gotEffect),
				("outbound GameLost reached client", gotGameLost),
				("LanDiscovery beacon produced a lobby entry", sawLobby),
			};

		foreach (var (name, ok) in checks)
		{
			GD.Print($"[RelayTest] {(ok ? "PASS" : "FAIL")} — {name}");
		}

		GD.Print(
			$"[RelayTest] {(isHost ? "host" : "client")}: {(pass ? "=== ALL PASS ===" : "=== FAILED ===")}"
		);

		GetTree().Quit(pass ? 0 : 1);
	}
}
