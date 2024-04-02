using System;
using Godot;

public abstract partial class Holdable : Node
{
    protected Node3D Player;
    protected Node3D Ghost;
    protected bool Holding = false;

    public override void _Ready()
    {
        Player = GetTree().CurrentScene.GetNode<Node3D>("Player");
        Ghost = GetTree().CurrentScene.GetNode<Node3D>("Ghost");

        GD.Print("Initialized holdable with player: " + Player + " and ghost: " + Ghost);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (
            Holding
            && (
                (
                    Input.IsActionJustPressed("LetGoOfItem")
                    && GetViewport().GuiGetFocusOwner() == null
                ) || Player.Get("dead").AsBool()
            )
        )
        {
            var forwardVector = -Player.GetNode<Node3D>("Head/Camera3d").GlobalTransform.Basis.Z;
            var parent = GetParent<RigidBody3D>();
            parent.Freeze = false;
            Holding = false;
            parent.ApplyImpulse((forwardVector * 0.25f));
        }

        if (
            Input.IsActionJustPressed("SecondaryInteractInHand")
            && Holding
            && GetViewport().GuiGetFocusOwner() == null
        )
        {
            secondaryInteract();
        }
    }

    public void Interact()
    {
        var parent = GetParent<RigidBody3D>();
        parent.Freeze = true;
        Holding = true;
    }

    public override void _Process(double delta)
    {
        if (Holding)
        {
            GetParent<Node3D>().GlobalTransform = Player
                .GetNode<Node3D>("Head/Camera3d/ItemAttachmentPoint")
                .GlobalTransform;
        }
    }

    public abstract string getStatus();

    public abstract void interact();
    public abstract void secondaryInteract();
}
