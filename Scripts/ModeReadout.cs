using System;
using Godot;

public partial class ModeReadout : RichTextLabel
{
    private double elapsed = 0;
    private double interval = 0.5;

    private int currentReadSpinnerFrame = 0;
    private int currentInterpretSpinnerFrame = 0;

    private string Mode = "READ";

    string[] spinnerFrames = new string[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    public override void _Ready()
    {
        EventBus.Get().LLMFirstResponseChunk += (chunk) =>
        {
            Mode = "INTERPRET";
        };

        EventBus.Get().LLMLastResponseChunk += (chunk) =>
        {
            Mode = "READ";
        };

        EventBus.Get().LLMResponseChunk += (chunk) =>
        {
            Mode = "INTERPRET";
            currentInterpretSpinnerFrame++;

            if (currentInterpretSpinnerFrame >= spinnerFrames.Length)
            {
                currentInterpretSpinnerFrame = 0;
            }
        };
    }

    public override void _Process(double delta)
    {
        elapsed += delta;

        if (elapsed >= interval)
        {
            elapsed = 0;

            if (Mode == "READ")
            {
                Text = "[b]" + spinnerFrames[currentReadSpinnerFrame++] + " READ[/b]";
            }
        }

        if (currentReadSpinnerFrame >= spinnerFrames.Length)
        {
            currentReadSpinnerFrame = 0;
        }

        if (Mode == "INTERPRET")
        {
            Text =
                "[b][color=#00ff00]"
                + spinnerFrames[currentInterpretSpinnerFrame]
                + " INTERPRET[/color][/b]";
        }
    }
}
