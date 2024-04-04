using System;
using Godot;

public partial class TemperatureReader : Holdable
{
    [Export]
    private Label3D display;

    [Export]
    private Label3D tick;

    [Export]
    private AudioStreamPlayer3D PowerSound;

    [Export]
    private AudioStreamPlayer3D TickSound;

    private FastNoiseLite noise = new FastNoiseLite();

    private bool Power = false;

    private float GetTemp()
    {
        var parent = GetParent<Node3D>();

        var e =
            (
                noise.GetNoise3D(
                    parent.GlobalTransform.Origin.X,
                    parent.GlobalTransform.Origin.Y,
                    parent.GlobalTransform.Origin.Z
                ) * 5
            ) + 20;

        var tempNodes = GetTree().GetNodesInGroup("evidence_temperature");

        bool tempIsClose = false;
        foreach (Node3D node in tempNodes)
        {
            var distance = parent.GlobalTransform.Origin.DistanceTo(node.GlobalTransform.Origin);

            if (distance < 3)
            {
                tempIsClose = true;
                break;
            }
        }

        var tempConstant = tempIsClose ? -25f : 0f;

        var chaseMultiplier = Ghost.Get("chasing").AsBool()
            ? (float)GD.RandRange(0.25f, 1.75f)
            : 1f;

        return Math.Clamp((e + tempConstant) * chaseMultiplier, -20, 40);
    }

    private double Elapsed;
    private double ElapsedForSound;
    private double UpdateInterval = 1f;

    private int tickIndicator = 0;

    private Color tickColor = Color.FromHtml("#5555ff");
    private Color white = Color.FromHtml("#ffffff");

    public override void _Process(double delta)
    {
        base._Process(delta);

        Elapsed += delta;

        if (!Power)
        {
            display.Text = "";
            tick.Text = "";
        }

        var temp = GetTemp();
        UpdateInterval = Ghost.Get("chasing").AsBool() ? 0.2f : 1.5f;

        if (Elapsed >= (UpdateInterval / 2))
        {
            Elapsed = 0;
            if (!Power)
                return;

            tickIndicator = (tickIndicator + 1) % 2;
            tick.Modulate = tickIndicator == 0 ? tickColor : white;
            string beat = tickIndicator == 0 ? "READ -" : "READ";
            tick.Text = beat;

            if (tickIndicator == 0)
            {
                TickSound.PitchScale = temp < 0 ? 1.75f : 3f;

                TickSound.Play(0);
            }

            display.Modulate = Color.FromHtml("#0000ff").Lerp(Color.FromHtml("#ffffff"), temp / 30);
            display.Text = temp.ToString("0.00") + "C";
        }
    }

    // public override void interact()
    // {
    //     var parent = GetParent<RigidBody3D>();
    //     parent.Freeze = true;
    //     Holding = true;
    // }

    public override void secondaryInteract()
    {
        PowerSound.Play(0);
        Power = !Power;
    }

    public override string getStatus()
    {
        return "";
    }
}
