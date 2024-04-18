using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Godot;

public partial class Logger : Node
{
    private string logFilePath;
    private StringBuilder logBuffer = new StringBuilder();

    private EventBus bus;

    private int BufferSize = 128;

    private void Write(string text)
    {
        logBuffer.AppendLine(Time.GetTicksUsec() + "|" + text);

        // If the buffer reaches a certain size, write to disk.
        if (logBuffer.Length >= BufferSize)
        {
            Flush();
        }
    }

    public void Flush()
    {
        if (logBuffer.Length == 0)
            return;

        // Asynchronously write the buffer to disk and clear it.
        string contentToWrite = logBuffer.ToString();
        logBuffer.Clear();

        Task.Run(() =>
        {
            File.AppendAllText(logFilePath, contentToWrite);
        });
    }

    public override void _ExitTree()
    {
        // Ensure we write any remaining messages when the node is removed.
        Flush();
    }

    public override void _Ready()
    {
        bus = EventBus.Get();

        string directory = Path.GetDirectoryName(OS.GetExecutablePath());
        string subdirectory = "Game Logs";
        string logFileName = "Game Log " + $"{DateTime.Now:yyyy-MM-dd HHmmss}" + ".txt";
        string logDirectory = Path.Combine(directory, subdirectory);
        logFilePath = Path.Combine(logDirectory, logFileName);
        Directory.CreateDirectory(logDirectory);

        GD.Print("Logging to: " + logFilePath);

        bus.ObjectInteraction += (string verb, string objectType, string target) =>
        {
            Write($"ObjectInteraction|{verb} {objectType} {target}");
        };

        bus.ObjectInteractionAcknowledged += (string verb, string objectType, string target) =>
        {
            Write($"ObjectInteractionAcknowledged|{verb} {objectType} {target}");
        };

        bus.GhostAction += (string verb, string arguments) =>
        {
            Write($"GhostAction|{verb} {arguments}");
        };

        bus.GameDataRead += (string data) =>
        {
            Write($"GameDataRead");
        };

        bus.LLMPrompted += (string prompt) =>
        {
            Write($"LLMPrompted");
        };

        bus.LLMFirstResponseChunk += (string chunk) =>
        {
            Write($"LLMFirstResponseChunk");
        };

        bus.LLMLastResponseChunk += (string chunk) =>
        {
            Write($"LLMLastResponseChunk");
        };

        bus.LLMFullResponse += (string message) =>
        {
            Write($"LLMFullResponse|{message}");
            Flush();
        };

        bus.PlayerTalked += (string message) =>
        {
            Write($"PlayerTalked|{message}");
            Flush();
        };

        bus.InterpreterCommandRecognized += (string command) =>
        {
            Write($"InterpreterCommandRecognized|{command}");
        };

        bus.NotableEventOccurred += (string message) =>
        {
            Write($"NotableEventOccurred|{message}");
        };

        bus.NotableEventOccurredSpecificTime += (string message, ulong time) =>
        {
            Write($"NotableEventOccurredSpecificTime|{message}| (TRUE TIME: {time}) ");
        };

        bus.SystemFeedback += (string message) =>
        {
            Write($"SystemFeedback|{message}");
            Flush();
        };

        bus.AmendSystemFeedback += (string message) =>
        {
            Write($"AmendSystemFeedback|{message}");
        };

        bus.GhostBackstory += (string message) =>
        {
            Write($"GhostBackstory|{message}");
        };

        bus.PlayerDecidedGhostType += (string message) =>
        {
            Write($"PlayerDecidedGhostType|{message}");
            Flush();
        };

        bus.GameWon += (string message) =>
        {
            Write($"GameWon|" + message);
            Flush();
        };

        bus.GameLost += (string message) =>
        {
            Write($"GameLost|" + message);
            Flush();
        };

        bus.EndgameSummary += (string message) =>
        {
            Write($"EndgameSummary|" + message);
            Flush();
        };
    }
}
