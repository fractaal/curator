using System;
using Godot;

public partial class FailureStatistics : Node
{
    private EventBus Bus;

    [Export]
    private Label Display;

    private int CommandsRecognized = 0;
    private int BaseFailures = 0;

    private int TargetResolutionFailures = 0;
    private int ObjectResolutionFailures = 0;
    private int NarrativeIntegrityFailures = 0;

    public override void _PhysicsProcess(double delta)
    {
        if (Display.Visible)
        {
            Display.Text = GetStatistics();
        }
    }

    public override void _Ready()
    {
        Bus = EventBus.Get();

        Bus.LLMLastResponseChunk += (chunk) =>
        {
            Bus.EmitSignal(EventBus.SignalName.LogFileMessage, GetStatistics().Replace("\n", ", "));
        };

        Bus.SystemFeedback += (message) =>
        {
            message = message.ToLower();

            if (message.Contains("target doesn't exist"))
            {
                TargetResolutionFailures++;
            }
            else if (message.Contains("object doesn't exist"))
            {
                ObjectResolutionFailures++;
            }
            else if (message.Contains("narrative integrity"))
            {
                NarrativeIntegrityFailures++;
            }
            else if (message.Contains("command doesn't exist"))
            {
                BaseFailures++;
            }
        };

        Bus.InterpreterCommandRecognized += (command) =>
        {
            CommandsRecognized++;
        };
    }

    public string GetStatistics()
    {
        int SucceededCommands =
            CommandsRecognized
            - TargetResolutionFailures
            - ObjectResolutionFailures
            - NarrativeIntegrityFailures;

        float SuccessRate = 0;

        if ((CommandsRecognized + BaseFailures) > 0)
        {
            SuccessRate = (float)SucceededCommands / (CommandsRecognized + BaseFailures) * 100;
        }

        string result =
            $"Commands recognized: {CommandsRecognized}\n"
            + $"Base failures: {BaseFailures}\n"
            + $"Target resolution failures: {TargetResolutionFailures}\n"
            + $"Object resolution failures: {ObjectResolutionFailures}\n"
            + $"Narrative integrity failures: {NarrativeIntegrityFailures}\n"
            + $"Success rate: {SuccessRate:0.##}%\n";

        return result;
    }
}
