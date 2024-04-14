using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Sensors : Node
{
    public struct EventMessage
    {
        public string content;
        public int count;
        public ulong time;
    }

    public Message CurrentGameMessage;
    public Message CurrentLLMMessage;

    private static int LoopCount = 0;

    private List<EventMessage> NotableEvents = new();
    private List<EventMessage> SystemFeedback = new();
    private List<Message> History = new();

    private Node3D Player;
    private Node3D Ghost;

    private EventBus Bus;

    [Export]
    private RichTextLabel DebugView;

    [Export]
    private RichTextLabel PromptDebugView;

    private LLMInterface Interface;
    private string SummarizerPrompt;
    private bool GameEnded = false;

    private ulong LLMFirstResponseChunkTime = 0;

    private ulong AIReasoningTime = 0;
    private bool DoneSummarizing = true;
    private Node GhostData;
    private double TickInterval = 0.5;
    private double TickElapsed = 0;

    private double SensorReadInterval = 17.5;
    private double SensorReadElapsed = 0;

    private bool LoopCompleted = true;

    private ulong LastTimeChased = 0;

    private bool AIEnabled = false;

    private string GhostBackstory = "No backstory yet...";

    private PlayerStats Stats;

    private bool PerformedInitialSilentAIEnable = false;

    private string SYSTEM_PROMPT = FileAccess
        .Open("res://DeclarativeGameInterface/prompts/Main.txt", FileAccess.ModeFlags.Read)
        .GetAsText();

    private string BEHAVIOR_PROMPT = FileAccess
        .Open(
            "res://DeclarativeGameInterface/prompts/BehaviorPrompt.txt",
            FileAccess.ModeFlags.Read
        )
        .GetAsText();

    private string GetGameInfo()
    {
        return $@"Room Information:
{Room.GetAllRoomInformation()}

Ghost Backstory:
{GhostBackstory}";
    }

    private string GetNextPrompt()
    {
        var result = "";

        var events =
            from EventMessage e in NotableEvents
            where e.time > LLMFirstResponseChunkTime
            where e.content.ToLower().Contains("player")
            select e;

        var systemFeedback =
            from EventMessage e in SystemFeedback
            where e.time > LLMFirstResponseChunkTime
            select e;

        result =
            $@"CURRENT TIME {Time.GetTicksMsec() / 1000f}s

{GetContextualAttentionMarkers()}

<SYSTEM_FEEDBACK>
{EventMessagesToNaturalLanguageSimple(systemFeedback.ToList())}
</SYSTEM_FEEDBACK>

<TIMELINE>
{EventMessagesToNaturalLanguageSimple(events.ToList())}
</TIMELINE>

{GetContextualAttentionMarkers()}
";
        return result;
    }

    private string GetNextPromptWithPlayerAndGhostStatus()
    {
        var result = "";

        var events =
            from EventMessage e in NotableEvents
            where e.time > LLMFirstResponseChunkTime
            where e.content.ToLower().Contains("player")
            select e;

        var systemFeedback =
            from EventMessage e in SystemFeedback
            where e.time > LLMFirstResponseChunkTime
            select e;

        result =
            $@"CURRENT TIME {Time.GetTicksMsec() / 1000f}s

{GetContextualAttentionMarkers()}

<SYSTEM_FEEDBACK>
{EventMessagesToNaturalLanguageSimple(systemFeedback.ToList())}
</SYSTEM_FEEDBACK>

<GHOST>
{Ghost.Call("getStatus").AsString()}
</GHOST>

<PLAYER>
{Player.Call("getStatus").AsString()}
</PLAYER>

<TIMELINE>
{EventMessagesToNaturalLanguageSimple(events.ToList())}
</TIMELINE>

{GetContextualAttentionMarkers()}
";
        return result;
    }

    private void SendDataToLLM()
    {
        var allRoomInfo = Room.GetAllRoomInformation();

        List<Message> messages =
            new()
            {
                new Message { role = "system", content = SYSTEM_PROMPT },
                new Message { role = "system", content = GetGameInfo() },
            };

        messages.AddRange(History.TakeLast(10).ToList());

        messages.AddRange(
            new List<Message>
            {
                new Message { role = "user", content = GetNextPromptWithPlayerAndGhostStatus() },
                new Message { role = "system", content = BEHAVIOR_PROMPT }
            }
        );

        PromptDebugView.Text = messages.Aggregate(
            "",
            (acc, message) =>
                acc
                + "\n [color=\"#ff0000\"][b]"
                + message.role
                + "[/b][/color]: "
                + message.content
                + "\n"
        );

        History.Add(new Message { role = "user", content = GetNextPrompt() });

        Interface.Send(messages);
    }

    private void OnNotableEventOccurred(string message, ulong time)
    {
        const ulong FiveSeconds = 5 * 1000; // Assuming time is in milliseconds
        int count = 1; // Starting with 1 for the current event

        // Find the last event of the same type
        EventMessage lastEvent =
            new()
            {
                content = "",
                count = 0,
                time = 0
            };

        try
        {
            NotableEvents.FindLast(e => e.content.Contains(message));
        }
        catch (Exception)
        {
            //
        }

        // Check if the last event exists and is within 5 seconds
        if (lastEvent.time != 0 && (time - lastEvent.time) <= FiveSeconds)
        {
            count += lastEvent.count; // Increment count from the last event
            NotableEvents.Remove(lastEvent); // Remove the last event as we'll replace it
        }

        // Regardless of whether we found a matching event within 5 seconds, add the new event
        NotableEvents.Add(
            new EventMessage
            {
                count = count,
                content = message,
                time = time
            }
        );
    }

    // Called when the node enters the scene tree for the first time.
    public override async void _Ready()
    {
        SummarizerPrompt = FileAccess
            .Open(
                "res://DeclarativeGameInterface/prompts/SummarizerPrompt.txt",
                FileAccess.ModeFlags.Read
            )
            .GetAsText();

        Bus = EventBus.Get();

        try
        {
            SensorReadInterval = float.Parse(Config.Get("SENSOR_READ_INTERVAL"));
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to parse SENSOR_READ_INTERVAL: " + e.Message);
        }

        Player = GetTree().CurrentScene.GetNode<Node3D>("Player");
        Stats = Player.GetNode<PlayerStats>("PlayerStats");

        Ghost = GetTree().CurrentScene.GetNode<Node3D>("Ghost");

        Interface = GetNode<LLMInterface>("/root/LLMInterface");
        GhostData = GetNode<Node>("/root/GhostData");

        Bus.ChaseEnded += () =>
        {
            LastTimeChased = Time.GetTicksMsec();
        };

        Bus.GhostTalked += (message) =>
        {
            NotableEvents.Add(
                new()
                {
                    content = "Ghost interaction: speakAsGhost(" + message + ")",
                    count = 1,
                    time = Time.GetTicksMsec()
                }
            );
        };

        Bus.PlayerTalked += (message) =>
        {
            NotableEvents.Add(
                new()
                {
                    content = "PLAYER TALKED: " + message,
                    count = 1,
                    time = Time.GetTicksMsec()
                }
            );
        };

        Bus.NotableEventOccurredSpecificTime += (message, time) =>
        {
            OnNotableEventOccurred(message, time);
        };

        Bus.NotableEventOccurred += (message) =>
        {
            OnNotableEventOccurred(message, Time.GetTicksMsec());
        };

        Bus.SystemFeedback += (message) =>
        {
            var time = Time.GetTicksMsec();
            const ulong FiveSeconds = 5 * 1000; // Assuming time is in milliseconds

            int count = 1; // Starting with 1 for the current event

            // Find the last event of the same type
            EventMessage lastEvent =
                new()
                {
                    content = "",
                    count = 0,
                    time = 0
                };

            try
            {
                SystemFeedback.FindLast(e => e.content.Contains(message));
            }
            catch (Exception)
            {
                //
            }

            // Check if the last event exists and is within 5 seconds
            if (lastEvent.time != 0 && (time - lastEvent.time) <= FiveSeconds)
            {
                count += lastEvent.count; // Increment count from the last event
                SystemFeedback.Remove(lastEvent); // Remove the last event as we'll replace it
            }

            // Regardless of whether we found a matching event within 5 seconds, add the new event
            SystemFeedback.Add(
                new EventMessage
                {
                    count = count,
                    content = message,
                    time = time
                }
            );

            SystemFeedback = SystemFeedback.TakeLast(7).ToList();
        };

        Bus.AmendSystemFeedback += (message) =>
        {
            var words = message.Split(" ");

            if (words.Length == 0)
            {
                return;
            }

            SystemFeedback.RemoveAll(
                _event => words.Any(word => _event.content.ToLower().Contains(word.ToLower()))
            );
        };

        Bus.LLMFirstResponseChunk += (chunk) =>
        {
            AIReasoningTime = Time.GetTicksMsec();
            LLMFirstResponseChunkTime = Time.GetTicksMsec();
        };

        Bus.LLMLastResponseChunk += (chunk) =>
        {
            LoopCompleted = true;
            LoopCount++;
        };

        Bus.GameWon += (string message) =>
        {
            GameEnded = true;
        };

        Bus.GameLost += (string message) =>
        {
            GameEnded = true;
        };

        Bus.LLMFullResponse += async (message) =>
        {
            if (!AIEnabled)
            {
                GD.Print("AI disabled, skipping LLM summarization.");
                DoneSummarizing = true;
                return;
            }

            if (message == "")
            {
                GD.Print("Empty message, skipping LLM summarization.");
                DoneSummarizing = true;
                return;
            }

            var response = await Interface.SendIsolated(
                new List<Message>
                {
                    new Message { role = "system", content = SummarizerPrompt },
                    new Message { role = "user", content = message }
                }
            );

            var events =
                from EventMessage e in NotableEvents
                where e.time > LLMFirstResponseChunkTime
                where e.content.ToLower().Contains("ghost")
                select e;

            History.Add(
                new Message
                {
                    role = "assistant",
                    content =
                        $@"{response}

{EventMessagesToNaturalLanguageSimple(events.ToList())}"
                }
            );

            DoneSummarizing = true;
        };

        await ToSignal(GetTree().CreateTimer(1), "timeout");

        var backstoryPrompt = FileAccess
            .Open(
                "res://DeclarativeGameInterface/prompts/BackstoryPrompt.txt",
                FileAccess.ModeFlags.Read
            )
            .GetAsText();

        var backstoryForPlayerPrompt = FileAccess
            .Open(
                "res://DeclarativeGameInterface/prompts/BackstoryForPlayerPrompt.txt",
                FileAccess.ModeFlags.Read
            )
            .GetAsText();

        GhostBackstory = await Interface.SendIsolated(
            new List<Message>
            {
                new Message { role = "system", content = backstoryPrompt },
                new Message { role = "user", content = Ghost.Call("getStatusStateless").ToString() }
            }
        );

        // var sanitizedBackstory = ghostData.Call("StripGhostTypes", ghostBackstory);
        var sanitizedBackstory = await Interface.SendIsolated(
            new List<Message>
            {
                new Message { role = "system", content = backstoryForPlayerPrompt },
                new Message { role = "user", content = GhostBackstory }
            }
        );

        Bus.EmitSignal(EventBus.SignalName.GhostBackstory, sanitizedBackstory);
    }

    public string EventMessagesToNaturalLanguageSimple(List<EventMessage> messages)
    {
        messages = new List<EventMessage>(messages);
        messages.Sort((m1, m2) => m1.time.CompareTo(m2.time));

        string naturalLanguage = "";

        foreach (var m in messages)
        {
            string count = m.count > 1 ? $" (x{m.count})" : "";
            naturalLanguage += "\t" + m.content + count + "\n";
        }

        if (naturalLanguage == "")
        {
            naturalLanguage += "None yet.";
        }

        return naturalLanguage;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("ToggleAI"))
        {
            AIEnabled = !AIEnabled;

            Bus.EmitSignal(
                EventBus.SignalName.ToastNotification,
                AIEnabled ? "AI Enabled" : "AI Disabled"
            );
        }

        if (Stats.HasPlayerSteppedInsideHouse && !PerformedInitialSilentAIEnable)
        {
            AIEnabled = true;
            PerformedInitialSilentAIEnable = true;
            GD.Print("Silently enabling AI because player has stepped inside the house.");
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        TickElapsed += delta;
        SensorReadElapsed += delta;

        if (DebugView != null && TickElapsed >= TickInterval)
        {
            DebugView.Text = GetNextPromptWithPlayerAndGhostStatus();
            TickElapsed = 0;
        }

        if (
            SensorReadElapsed >= SensorReadInterval
            || (Ghost.Get("chasing").AsBool() && SensorReadElapsed >= (SensorReadInterval * 0.5))
        )
        {
            SensorReadElapsed = 0;
            // return;

            if (!DoneSummarizing)
            {
                GD.Print("Summarizing in progress, skipping sensor read.");
                SensorReadElapsed = SensorReadInterval - 1;
                return;
            }

            if (!AIEnabled)
            {
                GD.Print("AI disabled, skipping sensor read.");
                SensorReadElapsed = SensorReadInterval - 1;
                return;
            }

            if (Player.Get("dead").AsBool())
            {
                GD.Print("Player dead, skipping sensor read.");
                SensorReadElapsed = SensorReadInterval - 1;
                return;
            }

            if (GameEnded)
            {
                GD.Print("Game ended, skipping sensor read.");
                SensorReadElapsed = SensorReadInterval - 1;
                return;
            }

            if (!LoopCompleted)
            {
                GD.Print("Loop not completed yet, skipping sensor read.");
                SensorReadElapsed = SensorReadInterval - 1;
                return;
            }

            SendDataToLLM();
            LoopCompleted = false;
        }
    }

    public string GetContextualAttentionMarkers()
    {
        string markers = "";

        if (Ghost.Get("chasing").AsBool())
        {
            markers +=
                "### 💥 GHOST IS CHASING PLAYER! GO CRAZY! - USE EVERYTHING IN YOUR ARSENAL! THROW OBJECTS! EXPLODE LIGHTS! BE CREATIVE! 💥 ###\n";
        }
        else if (
            !Ghost.Get("chasing").AsBool()
            && (Time.GetTicksMsec() - LastTimeChased) < 30000
            && LastTimeChased != 0
        )
        {
            markers +=
                "### 🛑 A CHASE HAS JUST ENDED - COOL OFF AND LET THE PLAYER BREATH FOR A MOMENT 🛑 ###\n";
        }

        if (Player.GetNode("Locator").Get("Room").AsString() == "None")
        {
            markers +=
                "### 🤚 PLAYER IS OUTSIDE THE HOUSE - GHOST CANNOT CHASE OUTSIDE THE HOUSE - BE SUBTLER, REFUSE TO ENGAGE, LURE PLAYER BACK IN, DON'T LOCK ENTRANCE DOOR ✋ ###\n";
        }

        return markers;
    }
}
