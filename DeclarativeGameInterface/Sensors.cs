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
    private ulong LLMPromptedTime = 0;

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

    private int MAX_HISTORY = 10;

    private ulong TimeSinceLastChase = 0;
    private ulong TimeSinceLastEvidenceDeposit = 0;

    private string GhostBackstory = "No backstory yet...";

    private PlayerStats Stats;

    private bool PerformedInitialSilentAIEnable = false;

    private int FearFactor = 0;

    private string SYSTEM_PROMPT = FileAccess
        .Open("res://DeclarativeGameInterface/prompts/Main.txt", FileAccess.ModeFlags.Read)
        .GetAsText();

    private string BEHAVIOR_PROMPT = FileAccess
        .Open(
            "res://DeclarativeGameInterface/prompts/BehaviorPrompt.txt",
            FileAccess.ModeFlags.Read
        )
        .GetAsText();

    private string ENDGAME_SUMMARY_PROMPT = FileAccess
        .Open(
            "res://DeclarativeGameInterface/prompts/EndGameSummaryPrompt.txt",
            FileAccess.ModeFlags.Read
        )
        .GetAsText();

    private string GetTimeSinceLastChase()
    {
        string result = "TIME SINCE LAST CHASE: ";

        var difference = (Time.GetTicksMsec() - LastTimeChased) / 1000f;

        if (difference > 100)
        {
            result += $"‼‼ {difference}s ‼‼ -- Now is the time to START A CHASE!";
        }
        else
        {
            result += $"{difference}s";
        }

        return result;
    }

    private string GetGameInfo()
    {
        return $@"Room Information:
{Room.GetAllRoomInformation()}

Ghost Sounds It Can Emit:
{Ghost.GetNode<Node>("GhostSounds").Call("get_available_sound_names").AsString()}

Ghost Backstory:
{GhostBackstory}";
    }

    private string GetSystemFeedback()
    {
        var result = "";

        var events = from EventMessage e in SystemFeedback where e.time > LLMPromptedTime select e;

        result = EventMessagesToNaturalLanguageSimple(events.ToList());

        if (!result.Contains("None yet"))
        {
            result =
                "Some things went wrong or could be improved upon in your last response. Take care to amend these in the future:\n"
                + result;
        }
        else
        {
            result = "";
        }

        return result;
    }

    private string GetArchivedPrompt()
    {
        var result = "";

        var events =
            from EventMessage e in NotableEvents
            where e.time > LLMPromptedTime
            where e.content.ToLower().Contains("player")
            select e;

        result =
            $@"TIME {Time.GetTicksMsec() / 1000f}s
# TIMELINE
{EventMessagesToNaturalLanguageSimple(events.ToList())}
---
{GetFearFactor()}
{GetTimeSinceLastChase()}
";
        return result;
    }

    private string GetNextPromptWithPlayerAndGhostStatus()
    {
        var result = "";

        var events =
            from EventMessage e in NotableEvents
            where e.time > LLMPromptedTime
            where e.content.ToLower().Contains("player")
            select e;

        var systemFeedback =
            from EventMessage e in SystemFeedback
            where e.time > LLMPromptedTime
            select e;

        result =
            $@"CURRENT TIME {Time.GetTicksMsec() / 1000f}s

{GetContextualAttentionMarkers()}

# GHOST
{Ghost.Call("getStatus").AsString()}

{GetContextualAttentionMarkers()}

# PLAYER
{Player.Call("getStatus").AsString()}

{GetContextualAttentionMarkers()}

# TIMELINE
{EventMessagesToNaturalLanguageSimple(events.ToList())}

{GetTimeSinceLastChase()}

{GetContextualAttentionMarkers()}

{GetFearFactor()}
";
        return result;
    }

    private void SendDataToLLM()
    {
        var allRoomInfo = Room.GetAllRoomInformation();
        var systemFeedback = GetSystemFeedback();

        List<Message> messages =
            new()
            {
                new Message { role = "system", content = SYSTEM_PROMPT },
                new Message { role = "user", content = GetGameInfo() },
            };

        var history = History.TakeLast(MAX_HISTORY);

        while (history.Count() > 0 && history.First().role == "assistant")
        {
            history = history.Skip(1).ToList();
        }

        if (history.Count() == 0)
        {
            GD.PushError("History list was exhausted? This should not happen!!!");
        }

        messages.AddRange(History.TakeLast(MAX_HISTORY).ToList());

        if (systemFeedback != "")
        {
            messages.Add(new Message { role = "user", content = systemFeedback });
        }

        var comprehensivePrompt =
            "[!!!] IMPORTANT INSTRUCTION: Unlike your previous responses, for the next one, be concise, but *COMPREHENSIVE*. Show your solution. Your latest one should be detailed, following step-by-step train-of-thought reasoning, as explained to you previously. Execute commands in-line with your reasoning to minimize latency.";

        messages.AddRange(
            new List<Message>
            {
                new Message { role = "user", content = BEHAVIOR_PROMPT },
                new Message { role = "user", content = GetNextPromptWithPlayerAndGhostStatus() },
                new Message { role = "user", content = comprehensivePrompt },
                new Message
                {
                    role = "user",
                    content = GetContextualAttentionMarkers() + " " + GetFearFactor()
                },
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

        if (systemFeedback != "")
        {
            History.Add(new Message { role = "user", content = systemFeedback });
        }
        History.Add(new Message { role = "user", content = GetArchivedPrompt() });

        Bus.EmitSignal(EventBus.SignalName.GameDataRead, "");

        LLMPromptedTime = Time.GetTicksMsec();
        Interface.Send(messages);
    }

    private void OnNotableEventOccurred(string message, ulong time)
    {
        const ulong FiveSeconds = 5 * 1000; // Assuming time is in milliseconds
        int count = 1; // Starting with 1 for the current event

        if (message.ToLower().Contains("deposited evidence"))
        {
            TimeSinceLastEvidenceDeposit = time;
        }

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

    public async void EndgameSummarization()
    {
        string allEvents = "";

        foreach (EventMessage e in NotableEvents)
        {
            allEvents += $"{e.time / 1000} - {e.content} ({e.count} times)\n";
        }

        var additionalGameInfo = "GAME INFORMATION:\n\n";

        additionalGameInfo += Ghost.Call("getStatusStateless").AsString() + "\n\n";
        additionalGameInfo += GetGameInfo() + "\n\n";

        var response = await Interface.SendIsolated(
            new List<Message>()
            {
                new Message { content = ENDGAME_SUMMARY_PROMPT, role = "system" },
                new Message { content = additionalGameInfo, role = "user" },
                new Message { content = "Events to summarize to follow: ", role = "user" },
                new Message { content = allEvents, role = "user" }
            }
        );

        Bus.EmitSignal(EventBus.SignalName.EndgameSummary, response);
    }

    public string GetFearFactor()
    {
        var chase = "";

        if (Ghost.Get("chasing").AsBool())
        {
            chase =
                " THE GHOST IS CHASING! YOU **NEED** TO KEEP FEAR FACTOR AT MAXIMUM! EXPLODE LIGHTS, THROW OBJECTS, PULL PLAYER -- ANYTHING!";
        }

        if (FearFactor >= 85)
        {
            return $"‼ FEAR FACTOR: 🚀🚀 {FearFactor} 🚀🚀 (**TERRIFYING.** AMAZING! BE RELENTLESS!)";
        }
        else if (FearFactor >= 60)
        {
            return $"‼ FEAR FACTOR: 💥💥 {FearFactor} 💥💥 (SCARY! THAT'S CRAZY!)" + chase;
        }
        else if (FearFactor >= 40)
        {
            return $"‼ FEAR FACTOR: {FearFactor} (INTENSE. KEEP IT UP!)" + chase;
        }
        else if (FearFactor > 0)
        {
            return $"‼ FEAR FACTOR: {FearFactor} (DANGEROUSLY LOW. FIX THIS, NOW!)" + chase;
        }
        else
        {
            return "‼ FEAR FACTOR: 👎 0 (BORING. USE COMMANDS AT YOUR DISPOSAL!)" + chase;
        }
    }

    // Called when the node enters the scene tree for the first time.
    public override async void _Ready()
    {
        try
        {
            MAX_HISTORY = int.Parse(Config.Get("MAX_HISTORY"));
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to parse MAX_HISTORY: " + e.Message);
            MAX_HISTORY = 10;
        }

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

        Bus.FearFactorChanged += (int value) =>
        {
            FearFactor = value;
        };

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
                    content = "🗣 (IMPORTANT!) PLAYER TALKED: " + message,
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

        Bus.ChaseStarted += () =>
        {
            LastTimeChased = Time.GetTicksMsec();
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
            EndgameSummarization();
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

Actions taken:
{EventMessagesToNaturalLanguageSimple(events.ToList())}"
                }
            );

            DoneSummarizing = true;
        };

        await ToSignal(GetTree().CreateTimer(1), "timeout");

        if (!Engine.IsEditorHint())
        {
            GenerateBackstory();
        }
    }

    public async void GenerateBackstory()
    {
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

        sanitizedBackstory = GhostData.Call("StripGhostTypes", sanitizedBackstory).AsString();

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
        if (Input.IsActionJustPressed("GenerateBackstory"))
        {
            GenerateBackstory();
        }

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
                "### 💥💥💥 GHOST IS CHASING PLAYER! GO CRAZY! - **INVOKE COMMANDS WITH RECKLESS ABANDON!** THROW OBJECTS! EXPLODE LIGHTS! **BE RELENTLESSLY AGGRESSIVE!**  **KEEP FEAR FACTOR AT 100!!!** 💥💥💥 ###\n";
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
                "### 🤚 PLAYER IS OUTSIDE THE HOUSE - GHOST CANNOT CHASE OUTSIDE THE HOUSE - BE SUBTLER, MAKE THE HOUSE MORE APPEALING, LURE PLAYER BACK IN, DON'T LOCK ENTRANCE DOOR ✋ ###\n";
        }

        return markers;
    }
}
