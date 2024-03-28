using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Sensors : Node
{
    private readonly List<string> _firstNames =
        new() { "John", "Jennifer", "Madison", "Mark", "Abrahm", "Dominic", "Kimi", };

    private readonly List<string> _lastNames =
        new() { "Black", "Brown", "Jackson", "Peralta", "Walker", "Carpenter", "Pedo" };

    private readonly List<string> _ghostTypes =
        new() { "Demon", "Wraith", "Phantom", "Shade", "Banshee" };

    private string ghostName = "";
    private string ghostType = "";
    private int ghostAge = 0;

    private List<string> NotableEvents = new();

    private string PlayerCurrentRoom = "";

    private EventBus bus;

    [Export]
    private RichTextLabel DebugView;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        bus = EventBus.Get();

        var random = new Random();
        ghostName =
            _firstNames[random.Next(0, _firstNames.Count)]
            + " "
            + _lastNames[random.Next(0, _lastNames.Count)];
        ghostType = _ghostTypes[random.Next(0, _ghostTypes.Count)];
        ghostAge = random.Next(20, 1000);

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
            if (!loopCompleted)
            {
                GD.Print("Loop not completed yet, skipping sensor read.");
                return;
            }

            loopCompleted = false;
            bus.EmitSignal(EventBus.SignalName.GameDataRead, GetGameInformationAndHistory());
        }
    }

    public string GetGameInformationAndHistory()
    {
        float time = Time.GetTicksMsec() / 1000;

        string mainInfo =
            $@"Time: {time}s
-- GHOST INFORMATION
Ghost Name: {ghostName}
Type: {ghostType}
Age: {ghostAge}

-- AVAILABLE ROOMS: {Room.GetRooms().Aggregate("", (acc, room) => acc + room.Name + ", ")}

-- PLAYER INFORMATION --
Current Room: {PlayerCurrentRoom}";

        string events = "\n-- NOTABLE EVENTS: \n";

        foreach (var e in NotableEvents)
        {
            events += e + "\n";
        }

        if (NotableEvents.Count == 0)
        {
            events += "No notable events yet.";
        }

        return mainInfo + "\n" + events;
    }
}
