using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Room : Area3D
{
    private static int i = 0;

    public static event Action<Room, Node3D> RoomEntered;
    public static event Action<Room, Node3D> RoomExited;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Monitoring = true;
        BodyEntered += (body) =>
        {
            if (body.HasMeta("isPlayer") && body.GetMeta("isPlayer").AsBool() == true)
            {
                i++;
                // LogManager.UpdateLog("roomEvent" + i, "Player entered " + Name);
                RoomEntered?.Invoke(this, body);
            }
        };

        BodyExited += (body) =>
        {
            try
            {
                if (body.HasMeta("isPlayer") && body.GetMeta("isPlayer").AsBool() == true)
                {
                    i++;
                    // LogManager.UpdateLog("roomEvent" + i, "Player exited " + Name);
                    RoomExited?.Invoke(this, body);
                }
            }
            catch (Exception) { }
        };
    }

    public static List<Room> GetRooms()
    {
        // So using the EventBus is a bit of a stretch here
        // but I need to access the GetTree method
        return EventBus.Get().GetTree().GetNodesInGroup("rooms").OfType<Room>().ToList();
    }
}
