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
    public delegate void PlayerEffectEventHandler(string verb, string arguments);

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
    public delegate void LLMFullResponseEventHandler(string response);

    [Signal]
    public delegate void InterpreterCommandRecognizedEventHandler(string command);

    [Signal]
    public delegate void NotableEventOccurredEventHandler(string message);

    [Signal]
    public delegate void NotableEventOccurredSpecificTimeEventHandler(string message, ulong time);

    [Signal]
    public delegate void SystemFeedbackEventHandler(string message);

    [Signal]
    public delegate void AmendSystemFeedbackEventHandler(string message);

    [Signal]
    public delegate void VoiceRecognitionEventHandler(bool isPartial, string message);

    [Signal]
    public delegate void GhostBackstoryEventHandler(string message);

    [Signal]
    public delegate void GhostInformationEventHandler(string message);

    [Signal]
    public delegate void PlayerDecidedGhostTypeEventHandler(string message);

    [Signal]
    public delegate void PlayerTalkedEventHandler(string message);

    [Signal]
    public delegate void GhostTalkedEventHandler(string message);

    [Signal]
    public delegate void GameWonEventHandler(string message);

    [Signal]
    public delegate void GameLostEventHandler(string message);

    [Signal]
    public delegate void EndgameSummaryEventHandler(string message);

    [Signal]
    public delegate void FearFactorChangedEventHandler(int fearFactor);

    [Signal]
    public delegate void ToastNotificationEventHandler(string message);

    // Some game-specific signals
    [Signal]
    public delegate void ChaseStartedEventHandler();

    [Signal]
    public delegate void ChaseEndedEventHandler();

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
