using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class RoomLocator : Node
{
    public string Room { get; private set; }

    public List<string> Rooms { get; private set; } = new();

    public Room RoomObject { get; private set; }
    public List<Room> RoomObjects { get; private set; } = new();

    private CollisionObject3D ReferenceBody;
    private Label3D DebugLabel;

    [Signal]
    public delegate void RoomFoundEventHandler(Room room);

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

        DebugLabel = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = false
        };

        // GetTree().CurrentScene.CallDeferred(nameof(AddChild), debugLabel);
        parent.AddChild(DebugLabel);

        DebugLabel.GlobalPosition = parent.GlobalPosition;
        DebugLabel.Scale = DebugLabel.Scale / parent.Scale;

        Godot.Collections.Array<Node> bodies;

        if (parent is CollisionObject3D)
        {
            bodies = new Godot.Collections.Array<Node> { parent };
        }
        else
        {
            bodies = parent.FindChildren("*", "CollisionObject3D", true);
        }

        if (bodies.Count == 0)
        {
            GD.PushWarning("RoomLocator has no CollisionObject3D children");
            DebugLabel.Text = "Can't detect room (No CollisionObject3D available)";
            return;
        }
        else
        {
            ReferenceBody = bodies[0] as CollisionObject3D;
            FindRoom();
        }
    }

    private void FindRoom()
    {
        Rooms.Clear();
        Room = "None";
        var rooms = GetTree().GetNodesInGroup("rooms");
        foreach (Area3D room in rooms)
        {
            var roomBodies = room.GetOverlappingBodies();

            for (int i = 0; i < roomBodies.Count; i++)
            {
                try
                {
                    var body = roomBodies[i];
                    if (body == ReferenceBody)
                    {
                        RoomObjects.Add(room as Room);
                        RoomObject = room as Room;

                        Room = room.Name;
                        Rooms.Add(Room);
                        DebugLabel.Text = "Room: " + Room;
                        GD.Print("Found room for " + GetParent().Name + ": " + Room);
                    }
                }
                catch (Exception)
                {
                    GD.Print("Skipping body - can't cast");
                }
            }
        }

        if (Room == "None")
        {
            if (DebugLabel != null)
            {
                DebugLabel.Text = "Can't detect room (No overlapping room)";
            }
            else
            {
                GD.Print("Can't detect room (No overlapping room)");
            }
        }
    }

    public override async void _Ready()
    {
        base._Ready();

        Room = "None";

        if (GetParent() is not Node3D)
        {
            GD.PushWarning("RoomLocator must be a child of a Node3D");
        }
        await ToSignal(GetTree().CreateTimer(1), "timeout");
        CallDeferred(nameof(deferredReady));
    }
}
