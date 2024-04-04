using System;
using System.Collections.Generic;
using Godot;

public abstract partial class Holdable : Node
{
    public enum RightOrLeft
    {
        RIGHT,
        LEFT
    }

    protected Node3D Player;
    protected Node3D Ghost;
    protected bool Holding = false;

    protected static Node3D attachmentPointRight;
    protected static Node3D attachmentPointLeft;

    protected static bool rightAvailable = true;
    protected static bool leftAvailable = true;

    protected RightOrLeft current;

    public override void _Ready()
    {
        if (attachmentPointLeft is null)
        {
            attachmentPointLeft = GetTree()
                .CurrentScene
                .GetNode<Node3D>("Player/Head/Camera3d/ItemAttachmentPointLeft");
        }

        if (attachmentPointRight is null)
        {
            attachmentPointRight = GetTree()
                .CurrentScene
                .GetNode<Node3D>("Player/Head/Camera3d/ItemAttachmentPointRight");
        }

        Player = GetTree().CurrentScene.GetNode<Node3D>("Player");
        Ghost = GetTree().CurrentScene.GetNode<Node3D>("Ghost");

        GD.Print("Initialized holdable with player: " + Player + " and ghost: " + Ghost);
    }

    public void LetGo(RightOrLeft hand)
    {
        var forwardVector = -Player.GetNode<Node3D>("Head/Camera3d").GlobalTransform.Basis.Z;
        var parent = GetParent<RigidBody3D>();
        parent.Freeze = false;
        Holding = false;
        if (hand == RightOrLeft.RIGHT)
        {
            rightAvailable = true;
        }
        else
        {
            leftAvailable = true;
        }
        parent.ApplyImpulse((forwardVector * 0.25f));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (
            Holding
            && (
                (
                    Input.IsActionJustPressed("LetGoRightHand")
                    && GetViewport().GuiGetFocusOwner() == null
                ) || Player.Get("dead").AsBool()
            )
        )
        {
            if (!rightAvailable && current == RightOrLeft.RIGHT)
            {
                GD.Print("Letting go of right attachment point");

                LetGo(RightOrLeft.RIGHT);
            }
        }

        if (
            Holding
            && (
                (
                    Input.IsActionJustPressed("LetGoLeftHand")
                    && GetViewport().GuiGetFocusOwner() == null
                ) || Player.Get("dead").AsBool()
            )
        )
        {
            if (!leftAvailable && current == RightOrLeft.LEFT)
            {
                GD.Print("Letting go of right attachment point");

                LetGo(RightOrLeft.LEFT);

                // var forwardVector = -Player
                //     .GetNode<Node3D>("Head/Camera3d")
                //     .GlobalTransform
                //     .Basis
                //     .Z;
                // var parent = GetParent<RigidBody3D>();
                // parent.Freeze = false;
                // Holding = false;
                // leftAvailable = true;
                // parent.ApplyImpulse((forwardVector * 0.25f));
            }
        }

        if (
            Input.IsActionJustPressed("SecondaryInteractInRightHand")
            && Holding
            && current == RightOrLeft.RIGHT
            && GetViewport().GuiGetFocusOwner() == null
        )
        {
            secondaryInteract();
        }

        if (
            Input.IsActionJustPressed("SecondaryInteractInLeftHand")
            && Holding
            && current == RightOrLeft.LEFT
            && GetViewport().GuiGetFocusOwner() == null
        )
        {
            secondaryInteract();
        }
    }

    public override void _Process(double delta)
    {
        if (Holding)
        {
            GetParent<Node3D>().GlobalTransform =
                current == RightOrLeft.RIGHT
                    ? attachmentPointRight.GlobalTransform
                    : attachmentPointLeft.GlobalTransform;
        }
    }

    public void interact()
    {
        if (rightAvailable)
        {
            GD.Print("Interacting with right hand");
            current = RightOrLeft.RIGHT;
            rightAvailable = false;
        }
        else if (leftAvailable)
        {
            GD.Print("Interacting with left hand");
            current = RightOrLeft.LEFT;
            leftAvailable = false;
        }
        else
        {
            GD.Print("Both hands are occupied");
            return;
        }

        var parent = GetParent<RigidBody3D>();
        parent.Freeze = true;
        Holding = true;
    }

    public abstract string getStatus();

    public abstract void secondaryInteract();
}
