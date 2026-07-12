using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>Stands in for Enemy.gd's hunt surface: records what the HuntTick's api
/// natives asked the engine to do, and mimics the ChaseEnded handshake on abort.</summary>
public partial class FakeHuntGhost : Node3D
{
	public bool hunt_in_grace = false;
	public double hunt_remaining_sec = 30.0;
	public bool hunt_lunge_ready = true;
	public string last_hunt_end_reason = "";
	public bool chasing = false;

	public Godot.Collections.Array<Vector3> MoveTargets = new();
	public int LungeCount = 0;
	public Godot.Collections.Array<string> Aborts = new();

	public void hunt_move_toward(Vector3 position)
	{
		MoveTargets.Add(position);
	}

	public bool hunt_lunge(Node3D target)
	{
		LungeCount++;
		return true;
	}

	public void abort_hunt(string reason)
	{
		Aborts.Add(reason);
		last_hunt_end_reason = reason;
		chasing = false;
		EventBus.Get().EmitSignal(EventBus.SignalName.ChaseEnded);
	}
}

/// <summary>A crew member with the script properties the hunt stack reads, plus a
/// physical body on the player collision layer so LOS raycasts can actually see it.</summary>
public partial class FakeHuntPlayer : Node3D
{
	public bool dead = false;
	public int player_number = 2;
}

public partial class FakeHuntLocator : Node
{
	public string Room = "Kitchen";
}

/// <summary>
/// Headless end-to-end proof of the HuntTick reflex layer, no game scene needed:
/// real HuntCore + GhostTools + EventBus, a scripted JS instinct file, synthetic
/// stimuli, and a fake Enemy hunt surface. Run with:
///
///   godot --headless res://DeclarativeGameInterface/MockTest/HuntTickMockTest.tscn
///
/// Exits 0 on PASS, 1 on FAIL.
/// </summary>
public partial class HuntTickMockTest : Node
{
	private EventBus bus;
	private GhostTools tools;
	private HuntCore hunt;
	private FakeHuntGhost ghost;
	private FakeHuntPlayer player;

	private readonly List<(string name, bool pass)> checks = new();
	private readonly List<string> feedback = new();

	private const string HuntScript =
		@"function update(ctx, api, inputs, state) {
			if (!state.init) { state.init = true; api.log('instincts online'); }
			if (ctx.lineOfSight) {
				api.lungeAt({ player: ctx.lineOfSight.player });
				return;
			}
			for (var i = 0; i < ctx.stimuli.length; i++) {
				var s = ctx.stimuli[i];
				if (s.kind === 'voice') {
					api.moveToward({ room: s.room });
					api.log('moving toward ' + s.room);
					return;
				}
			}
		}";

	private double elapsed;
	private bool concluded;

	public override void _Process(double delta)
	{
		elapsed += delta;
		if (!concluded && elapsed > 30.0)
		{
			GD.PrintErr("[HuntTickMockTest] TIMEOUT — test did not conclude in 30s");
			Conclude();
		}
	}

	private static string Text(ToolCallResult result) => HuntCore.ResultText(result);

	public override async void _Ready()
	{
		bus = EventBus.Get();
		bus.SystemFeedback += message => feedback.Add(message);

		// A fake room so moveToward({room}) resolves to a position.
		var kitchen = new Node3D { Name = "Kitchen", Position = new Vector3(5, 0, 5) };
		AddChild(kitchen);
		kitchen.AddToGroup("rooms");

		// The fake hunt surface + a registered crew member far away in the "Kitchen".
		ghost = new FakeHuntGhost { Name = "Ghost", Position = Vector3.Zero };
		AddChild(ghost);

		player = new FakeHuntPlayer { Name = "Player_2", Position = new Vector3(40, 0, 40) };
		var locator = new FakeHuntLocator { Name = "Locator" };
		player.AddChild(locator);
		// Torso height: LOS rays aim at player position +0.75, so the body sits there.
		var body = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0, Position = new Vector3(0, 0.75f, 0) };
		body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f } });
		player.AddChild(body);
		AddChild(player);
		PlayerManager.Get().RegisterPlayer(2, player);

		var integrity = GetNode<NarrativeIntegrity>("/root/NarrativeIntegrity");
		tools = new GhostTools(bus, integrity);
		hunt = new HuntCore();
		AddChild(hunt);
		hunt.Setup(bus, tools, null, ghost);
		tools.Hunt = hunt;

		await RunAllAsync();
	}

	private async Task RunAllAsync()
	{
		// 1. Compile gate: broken source is rejected, nothing installed.
		var bad = await tools.SetHuntScript("function update(ctx {");
		Check("broken script rejected with compile error",
			bad.Status == ToolCallStatus.Fail && Text(bad).Contains("COMPILATION FAILED"));
		Check("nothing installed after failed compile", !hunt.HasCompiledScript);

		// 2. No instincts -> no hunt.
		var blocked = await tools.ChasePlayerAsGhost("fast");
		Check("chase blocked without instincts",
			blocked.Status == ToolCallStatus.Fail && Text(blocked).Contains("HUNT BLOCKED"));

		// 3. Valid instincts install (linter warnings may ride along).
		var ok = await tools.SetHuntScript(HuntScript);
		Check("valid script installs", ok.Status == ToolCallStatus.Ok && hunt.HasCompiledScript);

		// 4. Patch atomicity: a bad patch leaves the running script byte-identical.
		var patch = await tools.PatchHuntScript("this string is not in the script", "x");
		var after = await tools.GetHuntScript();
		Check("bogus patch fails loudly",
			patch.Status == ToolCallStatus.Fail && Text(patch).Contains("PATCH FAILED"));
		Check("script unchanged after failed patch", Text(after) == HuntScript);

		// 5. Hunt session: a voice stimulus steers the ghost toward the speaker's room.
		ghost.chasing = true;
		bus.EmitSignal(EventBus.SignalName.ChaseStarted);
		bus.EmitSignal(EventBus.SignalName.PlayerTalked, "Player 2: \"marco\"");
		await WaitSeconds(0.5);
		Check("voice stimulus produced moveToward(Kitchen)",
			ghost.MoveTargets.Count > 0 && ghost.MoveTargets[0].IsEqualApprox(new Vector3(5, 0, 5)));

		// 6. Line of sight: player steps out in the open next to the ghost -> lunge.
		player.Position = new Vector3(2, 0, 0);
		await WaitSeconds(0.5);
		Check("line of sight produced a lunge", ghost.LungeCount > 0);

		// 7. Action budget: 8 tool calls per tick -> capped at 6 with a loud warning.
		player.Position = new Vector3(40, 0, 40); // break LOS so the spam path runs
		var spam = await tools.SetHuntScript(
			@"function update(ctx, api, inputs, state) {
				for (var i = 0; i < 8; i++) { api.flickerLights({ target: 'kitchen' }); }
				api.log('spamming');
			}");
		Check("spam script installs", spam.Status == ToolCallStatus.Ok);
		await WaitSeconds(0.5);
		Check("action budget warning emitted",
			feedback.Exists(f => f.Contains("Action budget")));

		// 8. Runtime error: the hunt aborts, loudly, and the script STAYS installed.
		var boom = await tools.SetHuntScript(
			"function update(ctx, api, inputs, state) { throw new Error('boom'); }");
		Check("throwing script installs (it compiles)", boom.Status == ToolCallStatus.Ok);
		await WaitSeconds(0.5);
		Check("runtime error aborted the hunt",
			ghost.Aborts.Count == 1 && ghost.Aborts[0] == "script_error");
		Check("crash surfaced as loud feedback",
			feedback.Exists(f => f.Contains("INSTINCTS CRASHED") && f.Contains("boom")));
		Check("script stays installed after crash (no-fallback ruling)", hunt.HasCompiledScript);

		// 9. The next hunt runs the same broken script and aborts again — no silent disable.
		ghost.chasing = true;
		bus.EmitSignal(EventBus.SignalName.ChaseStarted);
		await WaitSeconds(0.5);
		Check("next hunt runs the still-broken script and aborts again", ghost.Aborts.Count == 2);

		Conclude();
	}

	private async Task WaitSeconds(double seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), "timeout");
	}

	private void Check(string name, bool pass)
	{
		checks.Add((name, pass));
	}

	private void Conclude()
	{
		if (concluded)
		{
			return;
		}
		concluded = true;

		bool allPass = true;
		foreach (var (name, pass) in checks)
		{
			GD.Print($"[HuntTickMockTest] {(pass ? "PASS" : "FAIL")} — {name}");
			allPass &= pass;
		}
		GD.Print(allPass ? "[HuntTickMockTest] === ALL PASS ===" : "[HuntTickMockTest] === FAILED ===");
		GetTree().Quit(allPass ? 0 : 1);
	}
}
