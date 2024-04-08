using System;
using System.Collections.Generic;
using Godot;

public partial class InteractableRegistry : Node
{
    public static HashSet<string> Interactables { get; private set; }

    public static void Register(string name)
    {
        if (Interactables == null)
        {
            Interactables = new();
        }

        if (!Interactables.Contains(name.ToLower()))
        {
            GD.Print("Registering " + name + " as an interactable");
            Interactables.Add(name.ToLower());
        }
    }
}
