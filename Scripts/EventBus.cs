using Godot;

public partial class EventBus : Node
{
    [Signal]
    public delegate void ObjectInteractionEventHandler(
        string verb,
        string objectType,
        string target
    );

    [Signal]
    public delegate void ObjectInteractionAcknowledgedEventHandler(
        string verb,
        string objectType,
        string target
    );

    [Signal]
    public delegate void GhostActionEventHandler(string verb, string arguments);

    [Signal]
    public delegate void LogUpdatedEventHandler(string id, string message);

    [Signal]
    public delegate void GameDataReadEventHandler(string data);

    [Signal]
    public delegate void LLMPromptedEventHandler(string prompt);

    [Signal]
    public delegate void LLMFirstResponseChunkEventHandler(string chunk);

    [Signal]
    public delegate void LLMResponseChunkEventHandler(string chunk);

    [Signal]
    public delegate void LLMLastResponseChunkEventHandler(string chunk);

    [Signal]
    public delegate void InterpreterCommandRecognizedEventHandler(string command);

    [Signal]
    public delegate void NotableEventOccurredEventHandler(string message);

    [Signal]
    public delegate void SystemFeedbackEventHandler(string message);

    private static EventBus instance;

    public override void _Ready()
    {
        base._Ready();
        instance = this;
    }

    public static EventBus Get()
    {
        if (instance == null)
        {
            throw new System.Exception("EventBus should be an autoload");
        }
        return instance;
    }
}
