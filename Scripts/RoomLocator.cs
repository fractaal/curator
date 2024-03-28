using Godot;

public partial class RoomLocator : Node
{
    public string Room { get; private set; }

    private void deferredReady()
    {
        var parent = GetParent<Node3D>();

        var debugLabel = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = true
        };

        // GetTree().CurrentScene.CallDeferred(nameof(AddChild), debugLabel);
        parent.AddChild(debugLabel);

        debugLabel.GlobalPosition = parent.GlobalPosition;

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
                GD.Print(body.Name, referenceBody.Name);
                if (body == referenceBody)
                {
                    Room = room.Name;
                    debugLabel.Text = "Room: " + Room;
                    break;
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
