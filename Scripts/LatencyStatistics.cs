using System;
using Godot;

public partial class LatencyStatistics : Node
{
    private ulong loopStartTime;
    private ulong gameDataReadTime;
    private ulong llmPromptedTime;
    private ulong llmFirstResponseTime;
    private ulong llmLastResponseTime;

    private int interpreterCommandRecognizedCount;

    private ulong firstInterpreterCommandRecognizedTime;
    private ulong lastInterpreterCommandRecognizedTime;

    private void UpdateLog()
    {
        //         LogManager.UpdateLog(
        //             "latencyStats",
        //             $@"-- Latency Statistics --
        // Loop Start at - {loopStartTime}ms
        // Game Data Read at - {gameDataReadTime}ms
        // LLM Prompted at - {llmPromptedTime}ms
        // LLM First Response Chunk at - {llmFirstResponseTime}ms
        // LLM Last Response Chunk at - {llmLastReponseTime}ms

        // First Interpreter Command Recognized at - {firstInterpreterCommandRecognizedTime}ms
        // Last Interpreter Command Recognized at - {lastInterpreterCommandRecognizedTime}ms

        // Interpreter Command Recognized Count - {interpreterCommandRecognizedCount}
        // Interpreter Command Density - {(ulong)interpreterCommandRecognizedCount / ((llmLastReponseTime - loopStartTime) / 1000)} commands/sec
        // ");

        string displayLoopStartTime =
            loopStartTime == 0 ? "--" : (loopStartTime / 1000).ToString() + "s";
        string displayGameDataReadTime =
            gameDataReadTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (gameDataReadTime / 1000).ToString() + "s";
        string displayLLMPromptedTime =
            llmPromptedTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (llmPromptedTime / 1000).ToString() + "s";
        string displayLLMFirstResponseTime =
            llmFirstResponseTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (llmFirstResponseTime / 1000).ToString() + "s";
        string displayLLMLastResponseTime =
            llmLastResponseTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (llmLastResponseTime / 1000).ToString() + "s";
        string displayFirstInterpreterCommandRecognizedTime =
            firstInterpreterCommandRecognizedTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (firstInterpreterCommandRecognizedTime / 1000).ToString() + "s";
        string displayLastInterpreterCommandRecognizedTime =
            lastInterpreterCommandRecognizedTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (lastInterpreterCommandRecognizedTime / 1000).ToString() + "s";

        LogManager.UpdateLog(
            "latencyStatistics",
            $@"Loop Start at											  {displayLoopStartTime}
Game Data Read									{displayGameDataReadTime}
LLM Prompted										{displayLLMPromptedTime}
LLM First Response Chunk				{displayLLMFirstResponseTime}
LLM Last Response Chunk				{displayLLMLastResponseTime}

First Command Recognized				{displayFirstInterpreterCommandRecognizedTime}
Last Command Recognized				{displayLastInterpreterCommandRecognizedTime}

Command Recognized Count			{interpreterCommandRecognizedCount}
Command Density								{(double)interpreterCommandRecognizedCount / ((llmLastResponseTime - loopStartTime) / 1000)} actions / sec"
        );
    }

    public override void _Ready()
    {
        EventBus bus = EventBus.Get();

        bus.GameDataRead += (data) =>
        {
            if (loopStartTime == 0)
            {
                loopStartTime = Time.GetTicksMsec();
            }
            gameDataReadTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();
        };

        bus.LLMPrompted += (prompt) =>
        {
            llmPromptedTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();
        };

        bus.LLMFirstResponseChunk += (chunk) =>
        {
            llmFirstResponseTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();
        };

        bus.InterpreterCommandRecognized += (command) =>
        {
            interpreterCommandRecognizedCount++;
            if (interpreterCommandRecognizedCount == 1)
            {
                firstInterpreterCommandRecognizedTime = Time.GetTicksMsec() - loopStartTime;
            }
            lastInterpreterCommandRecognizedTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();
        };

        bus.LLMLastResponseChunk += (chunk) =>
        {
            llmLastResponseTime = Time.GetTicksMsec() - loopStartTime;
            UpdateLog();
            Reset();
        };
    }

    private void Reset()
    {
        loopStartTime = Time.GetTicksMsec();
        gameDataReadTime = 0;
        llmPromptedTime = 0;
        llmFirstResponseTime = 0;
        llmLastResponseTime = 0;
        interpreterCommandRecognizedCount = 0;
        firstInterpreterCommandRecognizedTime = 0;
        lastInterpreterCommandRecognizedTime = 0;
    }
}
