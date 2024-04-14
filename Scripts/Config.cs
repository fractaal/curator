using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Config : Node
{
    private static Dictionary<string, string> settings = new Dictionary<string, string>();

    private Config instance;

    public static bool SettingsFileMissing = false;

    public static string Get(string key)
    {
        if (settings.ContainsKey(key))
        {
            return settings[key];
        }
        return "";
    }

    public static void Set(string key, string value)
    {
        settings[key] = value;
    }

    public Config()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            GD.Print("Config instance already exists.");
            return;
        }

        string paramsFile = System
            .IO
            .Path
            .Combine(System.IO.Path.GetDirectoryName(OS.GetExecutablePath()), "settings.txt");

        if (System.IO.File.Exists(paramsFile))
        {
            string file = System.IO.File.ReadAllText(paramsFile);
            var lines = file.Split("\n");

            foreach (var line in lines)
            {
                if (line.StartsWith("[")) // Section marker, ignore
                {
                    continue;
                }

                if (line.StartsWith("#")) // Comment, ignore
                {
                    continue;
                }

                if (line.Trim() == "") // Empty line, ignore
                {
                    continue;
                }

                var parts = line.Split("=");

                if (parts.Count() > 1)
                {
                    settings[parts[0].Trim()] = parts[1].Trim();
                }
                else
                {
                    GD.PushWarning("Line in config file is not in the format key=value: " + line);
                }
            }

            SettingsFileMissing = false;
        }
        else
        {
            SettingsFileMissing = true;
        }
    }
}
