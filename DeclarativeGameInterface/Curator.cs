using System;
using Godot;

public partial class Curator : Node
{
    [Export]
    private LLMInterface llmInterface;

    private string PROMPT = "";

    public Curator()
    {
        var file = FileAccess.Open(
            "res://DeclarativeGameInterface/prompts/Main.txt",
            FileAccess.ModeFlags.Read
        );
        PROMPT = file.GetAsText();
    }

    private void _ready() { }
}
