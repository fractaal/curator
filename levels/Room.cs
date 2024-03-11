using System;
using Godot;

public partial class Room : Area3D
{
    private int i = 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Monitoring = true;
        BodyEntered += (body) =>
        {
            if (body.GetMeta("isPlayer").AsBool() == true)
            {
                GD.Print("player now entered ", Name);
            }
        };
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
