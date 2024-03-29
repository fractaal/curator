using Godot;

public partial class TargetResolution : Node
{
    private static TargetResolution instance;

    public override void _Ready()
    {
        instance = this;
    }

    public static Node GetTarget(string target)
    {
        target = target.Trim().ToLower();

        if (target == "player")
        {
            return instance.GetTree().CurrentScene.FindChild("Player");
        }
        else if (target == "ghost")
        {
            return instance.GetTree().CurrentScene.FindChild("Ghost");
            // Otherwise assume it's a room
        }
        else
        {
            if (target.StartsWith("in"))
            {
                target = target.Substring(2).Trim();
            }

            var rooms = instance.GetTree().GetNodesInGroup("rooms");

            foreach (Node room in rooms)
            {
                if (room.Name.ToString().ToLower().Contains(target))
                {
                    return room;
                }
            }
        }

        return null;
    }

    public static Vector3 GetTargetPosition(string target)
    {
        var node = GetTarget(target);

        if (node is Node3D)
        {
            return ((Node3D)node).GlobalTransform.Origin;
        }
        else
        {
            return new Vector3();
        }
    }
}
