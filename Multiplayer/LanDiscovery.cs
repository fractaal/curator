using System.Linq;
using Godot;

/// <summary>
/// Zero-infrastructure LAN lobby discovery. A host broadcasts a small UDP beacon
/// once a second; clients listen and surface a live lobby list in the join UI.
///
/// UDP broadcast does not cross subnets or VPNs (Tailscale etc.), so the manual
/// address field in MultiplayerHUD stays first-class alongside this.
/// </summary>
public partial class LanDiscovery : Node
{
	private const int DiscoveryPort = 4445;
	private const string Magic = "curator-lobby";
	private const double BeaconIntervalSeconds = 1.0;
	private const ulong LobbyExpiryMs = 3000;

	// Host side: broadcasts. Client side: listens. Never both at once.
	private PacketPeerUdp beacon;
	private PacketPeerUdp listener;

	private double beaconElapsed;
	private int maxPlayers;

	private sealed class Lobby
	{
		public int Players;
		public int MaxPlayers;
		public ulong LastSeenMs;
	}

	private readonly System.Collections.Generic.Dictionary<string, Lobby> lobbies = new();

	public void StartBeacon(int maxPlayersTotal)
	{
		StopDiscovery(); // a host doesn't browse

		maxPlayers = maxPlayersTotal;
		beacon = new PacketPeerUdp();
		beacon.SetBroadcastEnabled(true);
		beaconElapsed = BeaconIntervalSeconds; // first beacon goes out immediately
	}

	public void StopBeacon()
	{
		beacon?.Close();
		beacon = null;
	}

	public void StartDiscovery()
	{
		if (listener != null)
		{
			return;
		}

		listener = new PacketPeerUdp();
		if (listener.Bind(DiscoveryPort) != Error.Ok)
		{
			// Usually a second instance on the same machine (dev setup). The lobby
			// list stays empty; manual address join still works.
			GD.Print("LanDiscovery: could not bind discovery port, lobby list disabled");
			listener = null;
		}
	}

	public void StopDiscovery()
	{
		listener?.Close();
		listener = null;
		lobbies.Clear();
	}

	public override void _Process(double delta)
	{
		if (beacon != null)
		{
			beaconElapsed += delta;
			if (beaconElapsed >= BeaconIntervalSeconds)
			{
				beaconElapsed = 0;

				var payload = new Godot.Collections.Dictionary
				{
					{ "magic", Magic },
					{ "players", PlayerManager.Get().GetPlayerCount() },
					{ "max", maxPlayers },
				};
				var packet = Json.Stringify(payload).ToUtf8Buffer();

				beacon.SetDestAddress("255.255.255.255", DiscoveryPort);
				beacon.PutPacket(packet);

				// The broadcast copy isn't reliably delivered to listeners on this
				// same machine — send one on loopback too. It never leaves the host.
				beacon.SetDestAddress("127.0.0.1", DiscoveryPort);
				beacon.PutPacket(packet);
			}
		}

		if (listener != null)
		{
			while (listener.GetAvailablePacketCount() > 0)
			{
				var packet = listener.GetPacket();
				var address = listener.GetPacketIP();

				var parsed = Json.ParseString(packet.GetStringFromUtf8());
				if (parsed.VariantType != Variant.Type.Dictionary)
				{
					continue;
				}

				var dict = parsed.AsGodotDictionary();
				if (!dict.ContainsKey("magic") || dict["magic"].AsString() != Magic)
				{
					continue;
				}

				lobbies[address] = new Lobby
				{
					Players = dict.ContainsKey("players") ? dict["players"].AsInt32() : 0,
					MaxPlayers = dict.ContainsKey("max") ? dict["max"].AsInt32() : 0,
					LastSeenMs = Time.GetTicksMsec(),
				};
			}

			var now = Time.GetTicksMsec();
			var stale = lobbies
				.Where(kv => now - kv.Value.LastSeenMs > LobbyExpiryMs)
				.Select(kv => kv.Key)
				.ToList();

			foreach (var address in stale)
			{
				lobbies.Remove(address);
			}
		}
	}

	/// <summary>GDScript-friendly lobby list: [{ address, players, max }, ...]</summary>
	public Godot.Collections.Array GetLobbies()
	{
		var result = new Godot.Collections.Array();

		foreach (var kv in lobbies)
		{
			result.Add(
				new Godot.Collections.Dictionary
				{
					{ "address", kv.Key },
					{ "players", kv.Value.Players },
					{ "max", kv.Value.MaxPlayers },
				}
			);
		}

		return result;
	}
}
