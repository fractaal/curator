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
	protected bool IsBeingHeld = false;

	protected static Node3D attachmentPointRight;
	protected static Node3D attachmentPointLeft;

	protected static bool rightAvailable = true;
	protected static bool leftAvailable = true;

	protected RightOrLeft current;

	public override void _Ready()
	{
		Ghost = GetTree().CurrentScene.GetNode<Node3D>("Ghost");
		GD.Print("Initialized holdable");
	}

	public void LetGo(RightOrLeft hand)
	{
		var forwardVector = -Player.GetNode<Node3D>("Head/Camera3d").GlobalTransform.Basis.Z;
		var parent = GetParent<RigidBody3D>();
		parent.Freeze = false;
		IsBeingHeld = false;
		if (hand == RightOrLeft.RIGHT)
		{
			Player.Call("set_hand_availability", "right", true);
		}
		else
		{
			Player.Call("set_hand_availability", "left", true);
		}
		parent.ApplyImpulse(forwardVector * 0.25f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (
			IsBeingHeld
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
			IsBeingHeld
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
			}
		}

		if (
			Input.IsActionJustPressed("SecondaryInteractInRightHand")
			&& IsBeingHeld
			&& current == RightOrLeft.RIGHT
			&& GetViewport().GuiGetFocusOwner() == null
		)
		{
			GD.Print("RPCing secondary interact on client ", Multiplayer.GetUniqueId());
			Rpc("secondaryInteract");
		}

		if (
			Input.IsActionJustPressed("SecondaryInteractInLeftHand")
			&& IsBeingHeld
			&& current == RightOrLeft.LEFT
			&& GetViewport().GuiGetFocusOwner() == null
		)
		{
			GD.Print("RPCing secondary interact on client ", Multiplayer.GetUniqueId());
			Rpc("secondaryInteract");
		}
	}

	public override void _Process(double delta)
	{
		if (IsBeingHeld)
		{
			GetParent<Node3D>().GlobalTransform =
				current == RightOrLeft.RIGHT
					? attachmentPointRight.GlobalTransform
					: attachmentPointLeft.GlobalTransform;
		}
	}

	public void interact(Node3D player)
	{
		if (IsBeingHeld)
			return;

		var handAvailability = player.Call("get_hand_availability").AsGodotDictionary();
		var rightAvailable = handAvailability["right"].AsBool();
		var leftAvailable = handAvailability["left"].AsBool();

		
		attachmentPointRight = player.Get("left_attachment_point").AsGodotObject() as Node3D;
		attachmentPointLeft = player.Get("right_attachment_point").AsGodotObject() as Node3D;

		if (rightAvailable)
		{
			GD.Print("Interacting with right hand");
			player.Call("set_hand_availability", "right", false);
			current = RightOrLeft.RIGHT;
		}
		else if (leftAvailable)
		{
			GD.Print("Interacting with left hand");
			player.Call("set_hand_availability", "left", false);
			current = RightOrLeft.LEFT;
		}
		else
		{
			GD.Print("Both hands are occupied");
			return;
		}

		var parent = GetParent<RigidBody3D>();
		parent.Freeze = true;
		IsBeingHeld = true;
	}

	public abstract string getStatus();

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public abstract void secondaryInteract();
}
