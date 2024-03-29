using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Room : Area3D
{
    private static int i = 0;

    public static event Action<Room, Node3D> RoomEntered;
    public static event Action<Room, Node3D> RoomExited;

    public List<Node> Interactables { get; private set; }

    public Node findInteractableNode(Node node)
    {
        var parent = node;

        while (parent != null)
        {
            if (parent.IsInGroup("interactables"))
            {
                if (parent.HasNode("Interactable"))
                {
                    return parent.GetNode("Interactable");
                }
                else
                {
                    throw new Exception(
                        "Interactable node found but no Interactable script attached"
                    );
                }
            }
            ;
            parent = parent.GetParent();
        }

        throw new Exception("No interactable node found");
    }

    // Called when the node enters the scene tree for the first time.
    public async override void _Ready()
    {
        Interactables = new();

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

        // Get all interactables in the room
        // wait a second because scene tree may not be ready

        await ToSignal(GetTree().CreateTimer(2), "timeout");

        var bodies = GetOverlappingBodies();

        foreach (Node body in bodies)
        {
            try
            {
                var interactable = findInteractableNode(body);

                if (interactable != null)
                {
                    Interactables.Add(interactable);
                }
            }
            catch (Exception) { }
        }
    }

    public string GetInformation()
    {
        string info = "";

        info += $"- {Name}\n";

        foreach (Node interactable in Interactables)
        {
            try
            {
                if (interactable.HasMethod("getStatus"))
                {
                    string line = interactable.Call("getStatus").AsString();

                    if (line == "")
                    {
                        continue;
                    }

                    info += "\t" + line + "\n";
                }
                else
                {
                    GD.PushWarning(
                        "Interactable " + interactable.GetParent().Name + " has no getStatus method"
                    );
                }
            }
            catch (Exception e)
            {
                GD.PushWarning("Error getting interactable status: " + e.Message);
            }
        }

        return info.Trim();
    }

    public static List<Room> GetRooms()
    {
        // So using the EventBus is a bit of a stretch here
        // but I need to access the GetTree method
        return EventBus.Get().GetTree().GetNodesInGroup("rooms").OfType<Room>().ToList();
    }
}
