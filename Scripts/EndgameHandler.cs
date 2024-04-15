using System;
using Godot;

public partial class EndgameHandler : Node
{
    EventBus bus;
    Node3D player;
    Node3D ghost;

    public override void _Ready()
    {
        player = GetTree().CurrentScene.GetNode<Node3D>("Player");
        ghost = GetTree().CurrentScene.GetNode<Node3D>("Ghost");

        bus = EventBus.Get();

        AudioStreamPlayer audio = new AudioStreamPlayer();
        AudioStreamPlayer win = new AudioStreamPlayer();
        GetTree().CurrentScene.AddChild(audio);
        GetTree().CurrentScene.AddChild(win);
        audio.Stream = GD.Load<AudioStream>("res://Audio/ChosenSuspense.wav");
        win.Stream = GD.Load<AudioStream>("res://Audio/WinSong.wav");
        audio.VolumeDb = -10;
        win.VolumeDb = -10;

        bus.PlayerDecidedGhostType += async (message) =>
        {
            bus.EmitSignal(EventBus.SignalName.ObjectInteraction, "flicker", "lights", "all");
            audio.Play(0);

            GD.Print(
                "Player decided ghost type: "
                    + message
                    + ". Ghost is "
                    + ghost.Get("GhostType").AsString().Trim()
            );

            await ToSignal(GetTree().CreateTimer(3), "timeout");

            if (ghost.Get("GhostType").AsString().Trim() == message.Trim())
            {
                win.Play(0);
                GD.Print("yay you won");
                bus.EmitSignal(EventBus.SignalName.ObjectInteraction, "playerWon", "lights", "all");
                bus.EmitSignal(EventBus.SignalName.ObjectInteraction, "unlock", "doors", "all");
                bus.EmitSignal(EventBus.SignalName.ObjectInteraction, "open", "doors", "all");

                bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    "Player won (right ghost type) - picked "
                        + message
                        + ", ghost is "
                        + ghost.Get("GhostType").AsString().Trim()
                );

                bus.EmitSignal(EventBus.SignalName.GameWon, $"Right ghost type - picked {message}");
            }
            else
            {
                GD.Print("you lose");
                ghost.Call("update_target_location", ghost.GlobalPosition);
                ghost.Call("chase", "end");

                bus.EmitSignal(EventBus.SignalName.ObjectInteraction, "turnoff", "lights", "all");

                bus.EmitSignal(EventBus.SignalName.ObjectInteraction, "lock", "doors", "all");

                bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    "Player lost (wrong ghost type) - picked "
                        + message
                        + " but ghost is "
                        + ghost.Get("GhostType").AsString().Trim()
                );
                bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    "Because the player chose wrong, the ghost has become unbound from the house, allowing it to manifest outside!"
                );

                ghost.GlobalPosition = GetTree()
                    .CurrentScene
                    .GetNode<Node3D>("GhostEndgameSpawnpoint")
                    .GlobalPosition;

                bus.EmitSignal(
                    EventBus.SignalName.GameLost,
                    $"Wrong ghost type - picked {message} but ghost is {ghost.Get("GhostType").AsString().Trim()}"
                );

                // for (int i = 0; i < 90; i++)
                // {
                //     await ToSignal(GetTree().CreateTimer(0.05), "timeout");
                // }
            }
        };
    }
}
