using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class RoomLocator : Node
{
    public string Room { get; private set; }

    public List<string> Rooms { get; private set; } = new();

    public bool IsInRoom(string room)
    {
        foreach (string compareRoom in Rooms)
        {
            if (compareRoom.Trim().ToLower().Contains(room.Trim().ToLower()))
            {
                return true;
            }
        }

        return false;
    }

    private void deferredReady()
    {
        var parent = GetParent<Node3D>();

        var debugLabel = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = false
        };

        // GetTree().CurrentScene.CallDeferred(nameof(AddChild), debugLabel);
        parent.AddChild(debugLabel);

        debugLabel.GlobalPosition = parent.GlobalPosition;
        debugLabel.Scale = debugLabel.Scale / parent.Scale;

        var bodies = parent.FindChildren("*", "CollisionObject3D", true);
        CollisionObject3D referenceBody;

        if (bodies.Count == 0)
        {
            GD.PushWarning("RoomLocator has no CollisionObject3D children");
            debugLabel.Text = "Can't detect room (No CollisionObject3D available)";
            return;
        }
        else
        {
            referenceBody = bodies[0] as CollisionObject3D;
        }

        var rooms = GetTree().GetNodesInGroup("rooms");

        foreach (Area3D room in rooms)
        {
            var roomBodies = room.GetOverlappingBodies();

            foreach (Node body in roomBodies)
            {
                if (body == referenceBody)
                {
                    Room = room.Name;
                    Rooms.Add(Room);
                    debugLabel.Text = "Room: " + Room;
                }
            }
        }

        if (Room == "")
        {
            debugLabel.Text = "Can't detect room (No overlapping room)";
        }
    }

    public override async void _Ready()
    {
        base._Ready();

        Room = "";

        if (GetParent() is not Node3D)
        {
            GD.PushWarning("RoomLocator must be a child of a Node3D");
        }
        await ToSignal(GetTree().CreateTimer(1), "timeout");
        CallDeferred(nameof(deferredReady));
    }
}
