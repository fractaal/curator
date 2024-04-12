using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Sensors : Node
{
    private string ghostName = "";
    private string ghostType = "";
    private int ghostAge = 0;

    private static int loopCount = 0;

    private List<EventMessage> NotableEvents = new();

    private List<EventMessage> SystemFeedback = new();
    private List<EventMessage> PlayerSpeech = new();

    private Node3D player;

    private EventBus bus;

    [Export]
    private RichTextLabel DebugView;

    private LLMInterface llmInterface;

    private string SummarizerPrompt;
    private bool gameEnded = false;

    public struct EventMessage
    {
        public string content;
        public int count;
        public ulong time;
    }

    private ulong aiReasoningTime = 0;

    private Node ghostData;

    private void onNotableEventOccurred(string message, ulong time)
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

        NotableEvents = NotableEvents.TakeLast(60).ToList();
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

        bus = EventBus.Get();

        try
        {
            sensorReadInterval = float.Parse(Config.Get("SENSOR_READ_INTERVAL"));
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to parse SENSOR_READ_INTERVAL: " + e.Message);
        }

        player = GetTree().CurrentScene.GetNode<Node3D>("Player");
        playerStats = player.GetNode<PlayerStats>("PlayerStats");

        llmInterface = GetNode<LLMInterface>("/root/LLMInterface");
        ghostData = GetNode<Node>("/root/GhostData");

        bus.PlayerTalked += (message) =>
        {
            PlayerSpeech.Add(
                new()
                {
                    content = message,
                    count = 1,
                    time = Time.GetTicksMsec()
                }
            );
            PlayerSpeech = PlayerSpeech.TakeLast(15).ToList();
        };

        bus.NotableEventOccurredSpecificTime += (message, time) =>
        {
            onNotableEventOccurred(message, time);
        };

        bus.NotableEventOccurred += (message) =>
        {
            onNotableEventOccurred(message, Time.GetTicksMsec());
        };

        bus.SystemFeedback += (message) =>
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

        bus.AmendSystemFeedback += (message) =>
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

        bus.LLMFirstResponseChunk += (chunk) =>
        {
            aiReasoningTime = Time.GetTicksMsec();
        };

        bus.LLMLastResponseChunk += (chunk) =>
        {
            loopCompleted = true;
            loopCount++;
        };

        bus.GameWon += (string message) =>
        {
            gameEnded = true;
        };

        bus.GameLost += (string message) =>
        {
            gameEnded = true;
        };

        bus.LLMFullResponse += async (message) =>
        {
            if (!aiEnabled)
            {
                GD.Print("AI disabled, skipping LLM summarization.");
                return;
            }

            if (message == "")
            {
                GD.Print("Empty message, skipping LLM summarization.");
                return;
            }

            var response = await llmInterface.SendIsolated(
                new List<Message>
                {
                    new Message { role = "system", content = SummarizerPrompt },
                    new Message { role = "user", content = message }
                }
            );

            bus.EmitSignal(
                EventBus.SignalName.NotableEventOccurredSpecificTime,
                "Train of Thought / Intentions: " + response,
                aiReasoningTime
            );
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

        ghostBackstory = await llmInterface.SendIsolated(
            new List<Message>
            {
                new Message { role = "system", content = backstoryPrompt },
                new Message
                {
                    role = "user",
                    content = GetTree()
                        .CurrentScene
                        .GetNode("Ghost")
                        .Call("getStatusStateless")
                        .ToString()
                }
            }
        );

        // var sanitizedBackstory = ghostData.Call("StripGhostTypes", ghostBackstory);
        var sanitizedBackstory = await llmInterface.SendIsolated(
            new List<Message>
            {
                new Message { role = "system", content = backstoryForPlayerPrompt },
                new Message { role = "user", content = ghostBackstory }
            }
        );

        bus.EmitSignal(EventBus.SignalName.GhostBackstory, sanitizedBackstory);
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

    public string EventMessagesToNaturalLanguage(List<EventMessage> messages)
    {
        messages = new List<EventMessage>(messages);
        messages.Sort((m1, m2) => m1.time.CompareTo(m2.time));

        string naturalLanguage = "";

        double time = Time.GetTicksMsec();

        if (messages.Any(m => time - m.time > 30000))
        {
            naturalLanguage += "\n\t--- LONG TIME AGO (> 30s) ---\n";
        }
        foreach (var m in messages)
        {
            if (Time.GetTicksMsec() - m.time > 30000)
            {
                string count = m.count > 1 ? $" (x{m.count})" : "";
                naturalLanguage += "\t\t- " + m.content + count + "\n";
            }
        }

        // if (messages.Any(m => time - m.time > 30000))
        // {
        //     naturalLanguage += "\t</LONG TIME AGO>\n";
        // }

        if (messages.Any(m => time - m.time > 15000 && time - m.time <= 30000))
        {
            naturalLanguage += "\n\t--- WHILE AGO (> 15s) ---\n";
        }
        foreach (var m in messages)
        {
            if (time - m.time > 15000 && time - m.time <= 30000)
            {
                string count = m.count > 1 ? $" (x{m.count})" : "";
                naturalLanguage += "\t\t- " + m.content + count + "\n";
            }
        }

        // if (messages.Any(m => time - m.time > 15000 && time - m.time <= 30000))
        // {
        //     naturalLanguage += "\t</WHILE AGO>";
        // }

        if (messages.Any(m => time - m.time > 5000 && time - m.time <= 15000))
        {
            naturalLanguage += "\n\t--- FEW MOMENTS AGO (> 5s) ---\n";
        }
        foreach (var m in messages)
        {
            if (time - m.time > 5000 && time - m.time <= 15000)
            {
                string count = m.count > 1 ? $" (x{m.count})" : "";
                naturalLanguage += "\t\t- " + m.content + count + "\n";
            }
        }

        // if (messages.Any(m => time - m.time > 5000 && time - m.time <= 15000))
        // {
        //     naturalLanguage += "\t</FEW MOMENTS AGO>\n";
        // }

        if (messages.Any(m => time - m.time <= 5000))
        {
            naturalLanguage += "\n\t--- JUST NOW ---\n";
        }
        foreach (var m in messages)
        {
            if (time - m.time <= 5000)
            {
                string count = m.count > 1 ? $" (x{m.count})" : "";
                naturalLanguage += "\t\t- " + m.content + count + "\n";
            }
        }
        // if (messages.Any(m => time - m.time <= 5000))
        // {
        //     naturalLanguage += "\t</JUST NOW>\n";
        // }

        if (naturalLanguage == "")
        {
            naturalLanguage += "None yet.";
        }

        return naturalLanguage;
    }

    private double tickInterval = 0.5;
    private double tickElapsed = 0;

    private double sensorReadInterval = 17.5;
    private double sensorReadElapsed = 0;

    private bool loopCompleted = true;

    private ulong lastTimePoked = 0;

    private bool aiEnabled = false;

    private string ghostBackstory = "No backstory yet...";

    private PlayerStats playerStats;

    private bool PerformedInitialSilentAIEnable = false;

    public override async void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("ToggleAI"))
        {
            aiEnabled = !aiEnabled;

            bus.EmitSignal(
                EventBus.SignalName.ToastNotification,
                aiEnabled ? "AI Enabled" : "AI Disabled"
            );
        }

        if (playerStats.HasPlayerSteppedInsideHouse && !PerformedInitialSilentAIEnable)
        {
            aiEnabled = true;
            PerformedInitialSilentAIEnable = true;
            GD.Print("Silently enabling AI because player has stepped inside the house.");
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        tickElapsed += delta;
        sensorReadElapsed += delta;

        if (DebugView != null && tickElapsed >= tickInterval)
        {
            DebugView.Text = GetGameInformationAndHistory();
            // DebugView.ScrollToLine(DebugView.GetLineCount() - 1);
            tickElapsed = 0;
        }

        if (sensorReadElapsed >= sensorReadInterval)
        {
            sensorReadElapsed = 0;
            // return;

            if (!aiEnabled)
            {
                GD.Print("AI disabled, skipping sensor read.");
                return;
            }

            if (player.Get("dead").AsBool())
            {
                GD.Print("Player dead, skipping sensor read.");
                return;
            }

            if (gameEnded)
            {
                GD.Print("Game ended, skipping sensor read.");
                return;
            }

            if (!loopCompleted)
            {
                GD.Print("Loop not completed yet, skipping sensor read.");
                return;
            }

            loopCompleted = false;
            bus.EmitSignal(EventBus.SignalName.GameDataRead, GetGameInformationAndHistory());
        }
    }

    public string GetAllRoomInformation()
    {
        string info = "";

        foreach (var room in Room.GetRooms())
        {
            info += room.GetInformation() + "\n";
        }

        return info;
    }

    public string GetGameInformationAndHistory()
    {
        float time = Time.GetTicksMsec() / 1000;

        // string events = "";

        // foreach (var e in NotableEvents)
        // {
        //     string count = e.count > 1 ? $" (x{e.count})" : "";
        //     events += e.content + count + "\n";
        // }

        // if (NotableEvents.Count == 0)
        // {
        //     events += "No notable events yet.";
        // }

        // string systemFeedback = "";

        // foreach (var f in SystemFeedback)
        // {
        //     string count = f.count > 1 ? $" (x{f.count})" : "";
        //     systemFeedback += f.content + count + "\n";
        // }

        // if (SystemFeedback.Count == 0)
        // {
        //     systemFeedback += "No feedback yet.";
        // }

        string ghostStatus = "";

        string ghostChasingDecoration = GetTree()
            .CurrentScene
            .GetNode("Ghost")
            .Get("chasing")
            .AsBool()
            ? "### ⚠ GHOST IS CHASING PLAYER! GO CRAZY! - USE EVERYTHING IN YOUR ARSENAL! THROW OBJECTS! EXPLODE LIGHTS! BE CREATIVE! ⚠ ###"
            : "";

        string playerSpeech = "";

        try
        {
            ghostStatus = GetTree().CurrentScene.GetNode("Ghost").Call("getStatus").ToString();
        }
        catch (Exception e)
        {
            GD.Print("Error getting ghost status: ", e.Message);
        }

        return $@"<GAME_INFO>
CURRENT TIME: {time}s
{ghostChasingDecoration} 

<GHOST_BACKSTORY>
{ghostBackstory}
</GHOST_BACKSTORY>

{ghostChasingDecoration} 

<ROOMS>
{GetAllRoomInformation()}
</ROOMS>

{ghostChasingDecoration} 

<GHOST>
{ghostStatus}
</GHOST>

{ghostChasingDecoration} 

<PLAYER>
{playerStats.getStatus()}
</PLAYER>

{ghostChasingDecoration} 

<EVENTS>
{EventMessagesToNaturalLanguage(NotableEvents)}
</EVENTS>

{ghostChasingDecoration} 

<PLAYER_SPEECH> (Take care to listen and respond - or not! Your choice!)
{EventMessagesToNaturalLanguage(PlayerSpeech)}
</PLAYER_SPEECH>

{ghostChasingDecoration} 

<SYSTEM_FEEDBACK> (Take appropriate action, then amend with amendSystemFeedback(keyword).)
{EventMessagesToNaturalLanguageSimple(SystemFeedback)}
</SYSTEM_FEEDBACK>

{ghostChasingDecoration}
";
    }
}
