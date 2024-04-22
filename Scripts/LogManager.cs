using System;
using System.Collections.Generic;
using Godot;

public partial class LogManager : Node
{
    [Export]
    public RichTextLabel LatencyStatisticsUI;

    [Export]
    public RichTextLabel LLMResponseUI;

    [Export]
    public RichTextLabel LLMModelUI;

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

    public static void UpdateLog(string message)
    {
        UpdateLog(Guid.NewGuid().ToString(), message);
    }

    private void _updateLog(string id, string message)
    {
        // RichTextLabel label;

        // if (!logs.ContainsKey(id))
        // {
        //     label = _getLogUIElement();
        //     logs.Add(id, label);
        // }

        // label = logs[id];
        // label.Text = message;

        // ((ScrollContainer)LogUIContainer).ScrollVertical = 9999999;

        if (id == "latencyStatistics")
        {
            LatencyStatisticsUI.Text = message;
        }
        else if (id == "llmResponse")
        {
            LLMResponseUI.Text = message;
            LLMResponseUI.ScrollToLine(LLMResponseUI.GetLineCount());
        }
        else if (id == "llmModel")
        {
            LLMModelUI.Text = message;
        }
        else
        {
            GD.PushWarning("Unknown log id: " + id + " with message: " + message);
        }
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
