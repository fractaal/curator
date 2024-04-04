using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Sensors : Node
{
    private string ghostName = "";
    private string ghostType = "";
    private int ghostAge = 0;

    private static int loopCount = 0;

    private List<string> NotableEvents = new();

    private List<string> SystemFeedback = new();

    private string PlayerCurrentRoom = "";

    private Node3D player;

    private EventBus bus;

    [Export]
    private RichTextLabel DebugView;

    private LLMInterface llmInterface;

    [Export]
    private RichTextLabel StatusView;

    // Called when the node enters the scene tree for the first time.
    public override async void _Ready()
    {
        bus = EventBus.Get();

        StatusView = GetTree().CurrentScene.GetNode<RichTextLabel>("AIStatus");

        player = GetTree().CurrentScene.GetNode<Node3D>("Player");

        llmInterface = GetNode<LLMInterface>("/root/LLMInterface");

        // Attaching various object events to sensors
        Room.RoomEntered += (room, body) =>
        {
            float time = Time.GetTicksMsec() / 1000;
            PlayerCurrentRoom = room.Name;
        };

        bus.NotableEventOccurred += (message) =>
        {
            float time = Time.GetTicksMsec() / 1000;

            NotableEvents.Add($"{time}s: " + message);
            NotableEvents = NotableEvents.TakeLast(30).ToList();
            // PokePrompter();
        };

        bus.SystemFeedback += (message) =>
        {
            float time = Time.GetTicksMsec() / 1000;

            SystemFeedback.Add($"{time}s: " + message);
            SystemFeedback = SystemFeedback.TakeLast(5).ToList();
        };

        bus.LLMLastResponseChunk += (chunk) =>
        {
            loopCompleted = true;
            loopCount++;

            if (loopCount % 2 == 0 && SystemFeedback.Count > 0)
            {
                SystemFeedback.RemoveAt(0);
            }
        };

        await ToSignal(GetTree().CreateTimer(1), "timeout");

        var file = FileAccess.Open(
            "res://DeclarativeGameInterface/prompts/BackstoryPrompt.txt",
            FileAccess.ModeFlags.Read
        );
        var backstoryPrompt = file.GetAsText();

        ghostBackstory = await llmInterface.SendIsolated(
            new List<Message>
            {
                new Message { role = "system", content = backstoryPrompt },
                new Message
                {
                    role = "user",
                    content = GetTree()
                        .CurrentScene
                        .GetNode("Ghost")
                        .Call("getStatusStateless")
                        .ToString()
                }
            }
        );
    }

    private double tickInterval = 0.5;
    private double tickElapsed = 0;

    private double sensorReadInterval = 10;
    private double sensorReadElapsed = 0;

    private bool loopCompleted = true;

    private ulong lastTimePoked = 0;

    private bool aiEnabled = false;

    private string ghostBackstory = "No backstory yet...";

    public void PokePrompter()
    {
        if (Time.GetTicksMsec() - lastTimePoked < 5000)
        {
            GD.Print("Poke too soon, skipping.");
            return;
        }
        lastTimePoked = Time.GetTicksMsec();
        bus.EmitSignal(EventBus.SignalName.GameDataRead, GetGameInformationAndHistory());
    }

    Tween tween;

    public override async void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("ToggleAI"))
        {
            aiEnabled = !aiEnabled;

            StatusView.Text = aiEnabled ? "AI Enabled" : "AI Disabled";

            if (tween != null)
            {
                tween.Stop();
            }

            tween = CreateTween();

            StatusView.Modulate = new Color(1, 1, 1, 1);
            tween.TweenProperty(StatusView, "modulate", new Color(1, 1, 1, 0), 1);
            tween.Play();
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        tickElapsed += delta;
        sensorReadElapsed += delta;

        if (DebugView != null && tickElapsed >= tickInterval)
        {
            DebugView.Text = GetGameInformationAndHistory();
            DebugView.ScrollToLine(DebugView.GetLineCount() - 1);
            tickElapsed = 0;
        }

        if (sensorReadElapsed >= sensorReadInterval)
        {
            sensorReadElapsed = 0;
            // return;

            if (!aiEnabled)
            {
                GD.Print("AI disabled, skipping sensor read.");
                return;
            }

            if (player.Get("dead").AsBool())
            {
                GD.Print("Player dead, skipping sensor read.");
                return;
            }

            if (!loopCompleted)
            {
                GD.Print("Loop not completed yet, skipping sensor read.");
                return;
            }

            loopCompleted = false;
            bus.EmitSignal(EventBus.SignalName.GameDataRead, GetGameInformationAndHistory());
        }
    }

    public string GetAllRoomInformation()
    {
        string info = "";

        foreach (var room in Room.GetRooms())
        {
            info += room.GetInformation() + "\n";
        }

        return info;
    }

    public string GetGameInformationAndHistory()
    {
        float time = Time.GetTicksMsec() / 1000;

        string events = "";

        foreach (var e in NotableEvents)
        {
            events += e + "\n";
        }

        if (NotableEvents.Count == 0)
        {
            events += "No notable events yet.";
        }

        string systemFeedback = "";

        foreach (var f in SystemFeedback)
        {
            systemFeedback += f + "\n";
        }

        if (SystemFeedback.Count == 0)
        {
            systemFeedback += "No feedback yet.";
        }

        string ghostStatus = "";

        try
        {
            ghostStatus = GetTree().CurrentScene.GetNode("Ghost").Call("getStatus").ToString();
        }
        catch (Exception e)
        {
            GD.Print("Error getting ghost status: ", e.Message);
        }

        return $@"<GAME_INFO>
CURRENT TIME: {time}s

<GHOST>
{ghostStatus}
</GHOST>

<GHOST_BACKSTORY>
{ghostBackstory}
</GHOST_BACKSTORY>

<ROOMS>
{GetAllRoomInformation()}
</ROOMS>

<EVENTS>
{events}
</EVENTS>

<PLAYER>
{player.Call("getStatus")}
Current Room: {PlayerCurrentRoom}
</PLAYER>

<SYSTEM_FEEDBACK> (amend issues presented to you here)
{systemFeedback}
</SYSTEM_FEEDBACK>";
    }
}
