using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using Godot;

public partial class TargetResolution : Node
{
    private static TargetResolution instance;

    public override void _Ready()
    {
        instance = this;
    }

    private static readonly List<string> delimiters = new() { ":", "=" };

    private static readonly List<string> prepositions =
        new() { "on ", "at ", "near ", "around ", "nearby ", "the " };

    public static string NormalizeTargetString(string target)
    {
        target = target.Trim().ToLower();

        target = target.Replace("*", "");
        target = target.Replace("\"", "");

        target = prepositions.Aggregate(
            target,
            (current, preposition) => current.Replace(preposition, "")
        );

        if (target == "all")
        {
            return target;
        }

        if (target == "player")
        {
            return target;
        }

        if (target.StartsWith("in"))
        {
            target = target.Substring(2).Trim();
        }
        var split = delimiters.FirstOrDefault(delimiter => target.Contains(delimiter), "NONE");

        if (split != "NONE")
        {
            target = target.Split(split)[1].Trim();
        }

        var rooms = instance.GetTree().GetNodesInGroup("rooms");

        foreach (Node room in rooms)
        {
            if (Fuzz.PartialRatio(room.Name.ToString().ToLower().StripEdges(), target) > 80)
            {
                target = room.Name.ToString().ToLower();
            }
        }

        return target.Trim();
    }

    public static bool IsValidTarget(string target)
    {
        target = NormalizeTargetString(target);

        if (target == "all")
        {
            return true;
        }

        if (target == "player")
        {
            return true;
        }

        if (target == "ghost")
        {
            return true;
        }

        var rooms = instance.GetTree().GetNodesInGroup("rooms");

        foreach (Node room in rooms)
        {
            if (room.Name.ToString().ToLower().StripEdges().Contains(target))
            {
                return true;
            }
        }

        return false;
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
            EventBus
                .Get()
                .EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    "Target '"
                        + target
                        + "' is invalid. Maybe such a target doesn't exist. Please remember valid targets are only 'all', '<ROOM NAME>'"
                );
            return new Vector3();
        }
    }
}
