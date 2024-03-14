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

    [Export]
    private RichTextLabel DebugView;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
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

            LogManager.UpdateLog(
                Guid.NewGuid().ToString(),
                $"{time}s: Player entered " + room.Name
            );
            NotableEvents.Add($"{time}s: Player entered " + room.Name);
            NotableEvents = NotableEvents.TakeLast(10).ToList();
        };
        Room.RoomExited += (room, body) =>
        {
            float time = Time.GetTicksMsec() / 1000;

            LogManager.UpdateLog(Guid.NewGuid().ToString(), $"{time}s: Player exited " + room.Name);
            NotableEvents.Add($"{time}s: Player exited " + room.Name);
            NotableEvents = NotableEvents.TakeLast(10).ToList();
        };
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (DebugView != null)
        {
            DebugView.Text = GetGameInformationAndHistory();
            DebugView.ScrollToLine(DebugView.GetLineCount() - 1);
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
Age: {ghostAge}";

        string events = "\n-- NOTABLE EVENTS: \n";

        foreach (var e in NotableEvents)
        {
            events += e + "\n";
        }

        return mainInfo + "\n" + events;
    }
}
