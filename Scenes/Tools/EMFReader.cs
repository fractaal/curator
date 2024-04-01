using System;
using Godot;

public partial class EMFReader : Holdable
{
    [Export]
    private Label3D display;

    private Node3D Ghost;

    [Export]
    private AudioStreamPlayer3D PowerSound;

    [Export]
    private AudioStreamPlayer3D TickSound;

    private FastNoiseLite noise = new FastNoiseLite();

    private bool Power = false;

    public override void _Ready()
    {
        base._Ready();
        Ghost = GetTree().CurrentScene.GetNode<Node3D>("Ghost");
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

        var distanceToGhost = parent
            .GlobalTransform
            .Origin
            .DistanceTo(Ghost.GlobalTransform.Origin);

        var chaseMultiplier = 1f;

        if (Ghost.Get("chasing").AsBool())
        {
            chaseMultiplier = 3f;
        }

        var distanceToGhostMod = Math.Clamp((3 / distanceToGhost) * 1.5f, 0, 2);

        return Math.Clamp((e + distanceToGhostMod) * chaseMultiplier, 0, 6);
    }

    private double Elapsed;
    private double ElapsedForSound;
    private double UpdateInterval = 1f;

    public override void _Process(double delta)
    {
        base._Process(delta);

        Elapsed += delta;

        if (!Power)
        {
            display.Text = "";
        }

        var emf = GetEMF();
        UpdateInterval = 1f - (emf / 6);

        if (Elapsed >= UpdateInterval)
        {
            Elapsed = 0;
            if (!Power)
                return;

            TickSound.PitchScale = (float)(0.75 + (emf / 10));
            TickSound.Play(0);
            display.Modulate = Color.FromHtml("#ffffff").Lerp(Color.FromHtml("#ff0000"), emf / 6);
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
