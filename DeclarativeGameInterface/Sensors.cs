using System;
using System.Collections.Generic;
using Godot;

public partial class Sensors : Node
{
    private readonly List<string> _firstNames =
        new() { "John", "Jennifer", "Madison", "Mark", "Abrahm", "Dominic", "Kimi", };

    private readonly List<string> _lastNames =
        new() { "Black", "Brown", "Jackson", "Peralta", "Walker", "Carpenter", "Pedo" };

    private readonly List<string> _ghostTypes =
        new() { "Demon", "Wraith", "Phantom", "Shade", "Banshee" };

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }

    public string GetGameInformationAndHistory()
    {
        float time = Time.GetTicksMsec() / 1000;
        return "Time: " + time;
    }
}
