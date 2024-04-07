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

    private RichTextLabel FancyProgressBar;
    private RichTextLabel CurrentTime;

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
            loopStartTime == 0 ? "--" : (loopStartTime / 1000f).ToString("F") + "s";
        string displayGameDataReadTime =
            gameDataReadTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (gameDataReadTime / 1000f).ToString("F") + "s";
        string displayLLMPromptedTime =
            llmPromptedTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (llmPromptedTime / 1000f).ToString("F") + "s";
        string displayLLMFirstResponseTime =
            llmFirstResponseTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (llmFirstResponseTime / 1000f).ToString("F") + "s";
        string displayLLMLastResponseTime =
            llmLastResponseTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (llmLastResponseTime / 1000f).ToString("F") + "s";
        string displayFirstInterpreterCommandRecognizedTime =
            firstInterpreterCommandRecognizedTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (firstInterpreterCommandRecognizedTime / 1000f).ToString("F") + "s";
        string displayLastInterpreterCommandRecognizedTime =
            lastInterpreterCommandRecognizedTime == 0
                ? "[color=\"#ff8000\"]+--s[/color]"
                : "+" + (lastInterpreterCommandRecognizedTime / 1000f).ToString("F") + "s";

        LogManager.UpdateLog(
            "latencyStatistics",
            $@"Loop Start at                {displayLoopStartTime}
Game Data Read              {displayGameDataReadTime}
LLM Prompted                {displayLLMPromptedTime}

LLM First Response Chunk    {displayLLMFirstResponseTime}
First Command Recognized    {displayFirstInterpreterCommandRecognizedTime}

Last Command Recognized     {displayLastInterpreterCommandRecognizedTime}
LLM Last Response Chunk     {displayLLMLastResponseTime}

Command Recognized Count    {interpreterCommandRecognizedCount}"
        );
    }

    public override void _Ready()
    {
        EventBus bus = EventBus.Get();
        FancyProgressBar = GetTree()
            .CurrentScene
            .GetNode<RichTextLabel>("DebugUI/FancyProgressBar");

        CurrentTime = GetTree().CurrentScene.GetNode<RichTextLabel>("DebugUI/CurrentTime");

        bus.GameDataRead += (data) =>
        {
            Reset();
            if (loopStartTime == 0)
            {
                loopStartTime = Time.GetTicksMsec();
            }
            gameDataReadTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();

            DesiredProgressBarWidth = 0;
        };

        bus.LLMPrompted += (prompt) =>
        {
            llmPromptedTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();

            DesiredProgressBarWidth = 4;
        };

        bus.LLMFirstResponseChunk += (chunk) =>
        {
            llmFirstResponseTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();

            DesiredProgressBarWidth = 8;
        };

        bus.InterpreterCommandRecognized += (command) =>
        {
            interpreterCommandRecognizedCount++;
            if (interpreterCommandRecognizedCount == 1)
            {
                firstInterpreterCommandRecognizedTime = Time.GetTicksMsec() - loopStartTime;
                DesiredProgressBarWidth = 10;
            }
            DesiredProgressBarWidth = 14;
            lastInterpreterCommandRecognizedTime = Time.GetTicksMsec() - loopStartTime;

            UpdateLog();
        };

        bus.LLMLastResponseChunk += (chunk) =>
        {
            llmLastResponseTime = Time.GetTicksMsec() - loopStartTime;
            DesiredProgressBarWidth = 21;
            UpdateLog();
        };
    }

    private double ProgressBarTick = 0.025;
    private double ProgressBarElapsed = 200;

    private double TickIndicatorTick = 0.1f;
    private double TickIndicatorElapsed = 0;

    private int DesiredProgressBarWidth = 0;
    private int ActualProgressBarWidth = 0;

    private bool ProgressBarTickIndicator = false;

    public override void _Process(double delta)
    {
        ProgressBarElapsed += delta;
        TickIndicatorElapsed += delta;

        if (ProgressBarElapsed >= ProgressBarTick)
        {
            FancyProgressBar.Modulate = new Color(1, 0, 0, 1).Lerp(
                new Color(0, 1, 0, 1),
                (float)ActualProgressBarWidth / 21
            );

            ProgressBarElapsed = 0;

            ActualProgressBarWidth++;
            if (ActualProgressBarWidth > DesiredProgressBarWidth)
            {
                ActualProgressBarWidth = DesiredProgressBarWidth;
            }

            if (TickIndicatorElapsed > TickIndicatorTick)
            {
                TickIndicatorElapsed = 0;
                ProgressBarTickIndicator = !ProgressBarTickIndicator;
            }

            int tick = ProgressBarTickIndicator ? 1 : 0;

            if (ActualProgressBarWidth >= 21)
            {
                ActualProgressBarWidth = 21;
                FancyProgressBar.Text = "[b]" + new string('-', ActualProgressBarWidth) + "|[/b]";
            }
            else
            {
                FancyProgressBar.Text =
                    "[b]" + new string('-', ActualProgressBarWidth + tick) + ">[/b]";
            }

            CurrentTime.Text =
                "[right]"
                + ((Time.GetTicksMsec() - loopStartTime) / 1000f).ToString("F")
                + "s[/right]";
            CurrentTime.Position = new Vector2(730, 50 + (ActualProgressBarWidth * 7));
        }
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
