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

        SetCollisionMaskValue(8, true); // Physics objects layer

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
                    GD.Print(Name, ": added " + body.Name + " to interactables");
                }
            }
            catch (Exception) { }
        }
    }

    public Vector3 GetRandomPosition()
    {
        var possibleCollisionShapes = FindChildren("*", "CollisionShape3D");

        if (possibleCollisionShapes.Count > 0)
        {
            var collisionShape = (CollisionShape3D)possibleCollisionShapes[0];
            var shape = collisionShape.Shape;

            if (shape is BoxShape3D)
            {
                var extents = ((BoxShape3D)shape).Size;
                var center = GlobalTransform.Origin;

                var randomX = (float)GD.RandRange(-extents.X / 2, extents.X / 2) + center.X;
                var randomY = (float)GD.RandRange(-extents.Y / 2, extents.Y / 2) + center.Y;
                var randomZ = (float)GD.RandRange(-extents.Z / 2, extents.Z / 2) + center.Z;

                return new Vector3(randomX, randomY, randomZ);
            }
            else
            {
                GD.PushWarning("Using an approximation - shape isn't a box");
                // Use an approximation
                return GlobalTransform.Origin
                    + new Vector3(
                        (float)GD.RandRange(-10, 10),
                        (float)GD.RandRange(-10, 10),
                        (float)GD.RandRange(-10, 10)
                    );
            }
        }
        else
        {
            GD.PushWarning("Using an approximation - no collision shapes found");
            // Use an approximation
            return GlobalTransform.Origin
                + new Vector3(
                    (float)GD.RandRange(-10, 10),
                    (float)GD.RandRange(-10, 10),
                    (float)GD.RandRange(-10, 10)
                );
        }
    }

    public string GetInformation()
    {
        string info = "";

        var missingInteractables = InteractableRegistry.Interactables;

        info += $"<{Name}>\n";

        foreach (Node interactable in Interactables)
        {
            try
            {
                var type = interactable.Get("objectType").AsString();

                if (type == null)
                {
                    GD.PushWarning(
                        "Interactable "
                            + interactable.GetParent().Name
                            + " has no objectType property"
                    );
                }
                else
                {
                    missingInteractables.Remove(type);
                }

                if (interactable.HasMethod("getStatus"))
                {
                    string line = interactable.Call("getStatus").AsString();

                    if (info.Contains(line)) // Reduce redundancy
                    {
                        continue;
                    }

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

        for (int i = 0; i < missingInteractables.Count; i++)
        {
            info += $"\tNo {missingInteractables.ElementAt(i)}s in this room\n";
        }

        info += $"</{Name}>\n";

        return info.Trim();
    }

    public static List<Room> GetRooms()
    {
        // So using the EventBus is a bit of a stretch here
        // but I need to access the GetTree method
        return EventBus.Get().GetTree().GetNodesInGroup("rooms").OfType<Room>().ToList();
    }

    public List<Room> GetRoomsInstance()
    {
        return Room.GetRooms();
    }

    public static string GetAllRoomInformation()
    {
        string info = "";

        foreach (var room in GetRooms())
        {
            info += room.GetInformation() + "\n";
        }

        return info;
    }
}
