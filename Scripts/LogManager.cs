using System;
using System.Collections.Generic;
using Godot;

public partial class LogManager : Node
{
    [Export]
    public Control LogUIContainer;
    public static event Action<string, string> LogUpdated;
    private Dictionary<string, RichTextLabel> logs = new Dictionary<string, RichTextLabel>();

    private static LogManager _instance;

    public LogManager()
    {
        GD.Print("LogManager initialized");
        _instance = this;
    }

    public static void UpdateLog(string id, string message)
    {
        _instance.CallDeferred(nameof(_updateLog), id, message);
    }

    private void _updateLog(string id, string message)
    {
        RichTextLabel label;

        if (!logs.ContainsKey(id))
        {
            label = _getLogUIElement();
            logs.Add(id, label);
        }

        label = logs[id];
        label.Text = message;
    }

    private RichTextLabel _getLogUIElement()
    {
        var label = new RichTextLabel();

        LogUIContainer.AddChild(label);

        label.CustomMinimumSize = new Vector2(500, 0);
        label.BbcodeEnabled = true;
        label.FitContent = true;

        return label;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
