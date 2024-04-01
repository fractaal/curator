using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Sensors : Node
{
    private string ghostName = "";
    private string ghostType = "";
    private int ghostAge = 0;

    private List<string> NotableEvents = new();

    private List<string> SystemFeedback = new();

    private string PlayerCurrentRoom = "";

    private EventBus bus;

    [Export]
    private RichTextLabel DebugView;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        bus = EventBus.Get();

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
        };
    }

    private double tickInterval = 0.5;
    private double tickElapsed = 0;

    private double sensorReadInterval = 10;
    private double sensorReadElapsed = 0;

    private bool loopCompleted = true;

    private ulong lastTimePoked = 0;

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
            return;
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
            info += room.GetInformation() + "\n\n";
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

        return $@"CURRENT TIME: {time}s

-- GHOST INFORMATION
{ghostStatus}

-- ROOM INFORMATION --
{GetAllRoomInformation()}

-- NOTABLE EVENTS --
{events}

-- PLAYER INFORMATION --
Current Room: {PlayerCurrentRoom}

-- SYSTEM FEEDBACK (AMEND ANY ISSUES YOU SEE HERE IN THE FUTURE) --
{systemFeedback}";
    }
}
