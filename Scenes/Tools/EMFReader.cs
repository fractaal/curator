using System;
using Godot;

public partial class EMFReader : Holdable
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

    public override void _Ready()
    {
        base._Ready();
    }

    private float GetEMF()
    {
        var parent = GetParent<Node3D>();

        var e =
            noise.GetNoise3D(
                parent.GlobalTransform.Origin.X,
                parent.GlobalTransform.Origin.Y,
                parent.GlobalTransform.Origin.Z
            ) * 3;

        var emfNodes = GetTree().GetNodesInGroup("evidence_emf");

        bool emfIsClose = false;
        foreach (Node3D node in emfNodes)
        {
            var distance = parent.GlobalTransform.Origin.DistanceTo(node.GlobalTransform.Origin);

            if (distance < 2)
            {
                emfIsClose = true;
                break;
            }
        }

        var emfConstant = emfIsClose ? 4.5f : 0f;

        var distanceToGhost = parent
            .GlobalTransform
            .Origin
            .DistanceTo(Ghost.GlobalTransform.Origin);

        var chaseMultiplier = 1f;

        if (Ghost.Get("chasing").AsBool())
        {
            chaseMultiplier = (float)GD.RandRange(2.7f, 3.3f);
        }

        var distanceToGhostMod = Math.Clamp((3 / distanceToGhost) * 1.5f, 0, 2);

        return Math.Clamp((e + distanceToGhostMod + emfConstant) * chaseMultiplier, 0, 7);
    }

    private double Elapsed;
    private double ElapsedForSound;
    private double UpdateInterval = 1f;

    private int tickIndicator = 0;

    private Color tickColor = Color.FromHtml("#00ff00");
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

        var emf = GetEMF();
        UpdateInterval = 1f - (emf / 7);

        if (Elapsed >= (UpdateInterval / 2))
        {
            Elapsed = 0;
            if (!Power)
                return;

            tickIndicator = (tickIndicator + 1) % 2;
            tick.Modulate = tickIndicator == 0 ? tickColor : white;
            string beat = tickIndicator == 0 ? "TICK -" : "TICK";
            tick.Text = beat;

            if (tickIndicator == 0)
            {
                TickSound.PitchScale = (float)(0.75 + (emf / 10));
                TickSound.Play(0);
            }

            display.Modulate = Color.FromHtml("#ffffff").Lerp(Color.FromHtml("#ff0000"), emf / 7);
            display.Text = "EMF: " + emf.ToString("0.00");
        }
    }

    public override void interact()
    {
        var parent = GetParent<RigidBody3D>();
        parent.Freeze = true;
        Holding = true;
    }

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
