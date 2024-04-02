using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public partial class SpiritBox : Holdable
{
    [Export]
    private Label3D topCaption;

    [Export]
    private Label3D ghostSpeech;

    [Export]
    private Label3D tick;

    [Export]
    private Label3D spinner;

    [Export]
    private AudioStreamPlayer3D PowerSound;

    [Export]
    private AudioStreamPlayer3D TickSound;

    private bool Power = false;

    // Add an AudioStreamPlayer3D for playing back the ghost speech
    [Export]
    private AudioStreamPlayer3D GhostSpeechPlayer;

    private EventBus bus;

    private event Action<string> TTSFileReady;

    private int currentIdleSpinnerFrame = 0;
    private int currentInterpretSpinnerFrame = 0;

    private string Mode = "IDLE";

    string[] spinnerFrames = new string[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    public override void _Ready()
    {
        base._Ready();

        bus = EventBus.Get();

        bus.GhostAction += async (verb, arguments) =>
        {
            if (verb == "speakAsGhost" && Power)
            {
                Mode = "INTERPRET";
                topCaption.Text = "SPECTRAL VOICE DETECTED";
                arguments = Regex.Replace(arguments, "\"", "");
                arguments = Regex.Replace(arguments.ToLower(), "player", "");
                ghostSpeech.Text = arguments;

                var tokenized = FakeTokenize(RemoveNonAlphabetical(arguments));

                string fullText = "";

                GenerateSpeechAudio(arguments);

                foreach (var token in tokenized)
                {
                    if (!Power)
                        return;
                    fullText += token.ToUpper();
                    fullText = fullText.TakeLast(140).Aggregate("", (acc, c) => acc + c);
                    ghostSpeech.Text = fullText + GetRandomCharacters();
                    await ToSignal(GetTree().CreateTimer(GD.Randf() % 0.125, false), "timeout");
                    TickSound.Play(0);
                    tick.Text = "SPEAKING";
                    await ToSignal(GetTree().CreateTimer(0.125, false), "timeout");
                    tick.Text = "";
                    currentInterpretSpinnerFrame =
                        (currentInterpretSpinnerFrame + 1) % spinnerFrames.Length;
                }

                ghostSpeech.Text = fullText;

                await ToSignal(GetTree().CreateTimer(5, false), "timeout");
                ghostSpeech.Text = "";
                Mode = "IDLE";
                topCaption.Text = "ANALYZING SPECTRAL DATA";
            }
        };

        TTSFileReady += (audioFilePath) =>
        {
            CallDeferred(nameof(OnTTSFileReady), audioFilePath);
        };
    }

    private double IdleFramesInterval = 0.5;
    private double IdleFramesElapsed = 0;

    private double ChaseUpdateInterval = 0.1f;
    private double ChaseUpdateElapsed = 0f;
    private bool HasSetModeIdleAfterChase = false;

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        IdleFramesElapsed += delta;
        if (Power)
        {
            if (Mode == "IDLE")
            {
                topCaption.Text = "ANALYZING SPECTRAL DATA";
                spinner.Modulate = Color.FromHtml("#ffffff");
                spinner.Text = spinnerFrames[currentIdleSpinnerFrame];
                if (IdleFramesElapsed >= IdleFramesInterval)
                {
                    IdleFramesElapsed = 0;
                    currentIdleSpinnerFrame = (currentIdleSpinnerFrame + 1) % spinnerFrames.Length;
                }
            }
            else if (Mode == "INTERPRET")
            {
                spinner.Modulate = Color.FromHtml("#5555ff");
                spinner.Text = spinnerFrames[currentInterpretSpinnerFrame];
            }

            if (Ghost.Get("chasing").AsBool())
            {
                ChaseUpdateElapsed += delta;
                HasSetModeIdleAfterChase = false;

                if (ChaseUpdateElapsed >= ChaseUpdateInterval)
                {
                    ChaseUpdateElapsed = 0;
                    ChaseUpdateInterval = GD.RandRange(0.01f, 0.1f);
                    TickSound.PitchScale = (float)GD.RandRange(1.25f, 2f);
                    Mode = "INTERPRET";
                    topCaption.Text = "SPECTRAL VOICE DETECTED";

                    currentInterpretSpinnerFrame =
                        (currentInterpretSpinnerFrame + 1) % spinnerFrames.Length;

                    string fullText = GetRandomCharacters(10, GD.RandRange(10, 140));

                    var doATick = GD.Randf() > 0.5;

                    tick.Text = doATick ? "SPEAKING" : "";

                    if (doATick)
                        TickSound.Play(0);

                    ghostSpeech.Text = fullText;
                }
            }
            else
            {
                TickSound.PitchScale = 1f;
                if (!HasSetModeIdleAfterChase)
                {
                    HasSetModeIdleAfterChase = true;
                    ghostSpeech.Text = "";
                    Mode = "IDLE";
                }
            }
        }
        else
        {
            spinner.Text = "";
            topCaption.Text = "";
            ghostSpeech.Text = "";
            tick.Text = "";
        }
    }

    public List<string> FakeTokenize(string text)
    {
        int currentLength = 0;
        int maxLength = text.Length;

        List<string> tokenized = new List<string>();

        while (currentLength < maxLength)
        {
            int interval = (int)(GD.Randi() % 3 + 2); // GD.Randi() range is [2, 4]
            int start = currentLength;
            tokenized.Add(text.Substring(start, Mathf.Min(interval, maxLength - start)));
            currentLength += interval;
        }

        return tokenized;
    }

    private string GetRandomCharacters(int min, int max)
    {
        Random random = new Random();
        int length = random.Next(min, max); // Generate a random length between 2 and 3
        string characters = "abcdefghijklmnopqrstuvwxyz";
        string result = "";

        for (int i = 0; i < length; i++)
        {
            int index = random.Next(0, characters.Length);
            result += characters[index];
        }

        return result.ToUpper();
    }

    private string GetRandomCharacters()
    {
        return GetRandomCharacters(2, 4);
    }

    public string RemoveNonAlphabetical(string input)
    {
        // This regex matches any character that is NOT a letter (a-z, A-Z) or a space.
        // The "^" inside the character class ([]) negates the character class, matching anything not listed.
        string pattern = "[^a-zA-Z ]";

        // Replace matches of the pattern with an empty string.
        string result = Regex.Replace(input, pattern, "");

        return result;
    }

    public void OnTTSFileReady(string path)
    {
        var bytes = File.ReadAllBytes(path);

        var stream = new AudioStreamWav();
        stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
        stream.Data = bytes;
        stream.MixRate = 22050;
        stream.Stereo = false;

        GhostSpeechPlayer.Stream = stream;
        GhostSpeechPlayer.Play();
    }

    public override void interact()
    {
        var parent = GetParent<RigidBody3D>();
        parent.Freeze = true;
        Holding = true;
    }

    public override void secondaryInteract()
    {
        PowerSound.Play(0);
        Power = !Power;

        if (!Power)
        {
            GhostSpeechPlayer.Stop();
        }
    }

    public override string getStatus()
    {
        return "";
    }

    public void GenerateSpeechAudio(string text)
    {
        Thread thread = new Thread(() => SynthesizeSpeech(text));
        thread.Start();
    }

    private void SynthesizeSpeech(string text)
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), "ghost_speech.wav");
        // Ensure espeak-ng is installed and correctly configured in your macOS environment
        string binForMacOS = "/opt/homebrew/bin/espeak";
        string binForWindows = "espeak-ng";

        string command = $"\"{text}\" -s 140 -p 20 -g 10 -w \"{tempFilePath}\"";

        try
        {
            using (Process process = new Process())
            {
                // For macOS (or Unix-like systems), you don't use cmd.exe. Instead, you can directly set the FileName to the command or use /bin/bash
                // Check if running on macOS
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    process.StartInfo.FileName = "/opt/homebrew/bin/espeak";
                    process.StartInfo.Arguments = $"{command}";
                    GD.Print("Using OSX mode " + process.StartInfo.Arguments);
                }
                else // Assuming Windows if not macOS for simplicity; adjust as needed for other platforms
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c {binForWindows} {command}";
                    GD.Print("Using windows mode command " + process.StartInfo.Arguments);
                }

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit(); // This will not block the main thread

                if (process.ExitCode == 0)
                {
                    GD.Print($"Audio generated at: {tempFilePath}");
                    // Here you might want to signal the main thread that the audio is ready
                    TTSFileReady?.Invoke(tempFilePath);
                }
                else
                {
                    GD.Print($"espeak-ng process exited with error code: {process.ExitCode}");
                }
            }
        }
        catch (Exception e)
        {
            GD.Print($"Failed to generate speech audio: {e.Message}");
        }
    }
}
