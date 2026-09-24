using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace RussianTranslation
{
    /// <summary>
    /// Builds the Russian string table: the game's English with the translation laid over it key
    /// by key, so a string the translation lacks shows in English instead of NO_DATA.
    /// </summary>
    internal static class RussianStrings
    {
        /// <summary>
        /// The code the button saves in the game settings, in the game's own form ("Ukrainian").
        /// Without the plugin the game finds no such asset and falls back to English.
        /// </summary>
        public const string LanguageCode = "Russian";

        private const string FileName = "Russian.json";
        private const string ReportFileName = "untranslated.json";
        private const int DetailLimit = 10;

        // Formatter.ReplaceColorCodes turns these into colors. Any other capitalized or numeric <Word> is a
        // value the game's code fills in (<Credits>, <Name>, <0>...), so it must survive translation.
        // Lowercase tags (<b>, <color=...>) are TextMeshPro's own.
        private static readonly HashSet<string> ColorTags =
            ["Key", "Command", "Scary", "Error", "Secret", "Task", "Inactive", "Tutorial"];
        private static readonly Regex TagPattern = new(@"<([A-Z0-9]\w*)>", RegexOptions.Compiled);

        private static Dictionary<string, string>? _embedded;

        private sealed class Stats
        {
            public readonly HashSet<string> Translated = [];
            public readonly List<string> Obsolete = [];
            public readonly List<string> Placeholders = [];
        }

        public static Dictionary<string, string> Build()
        {
            Dictionary<string, string> english = ResourceLoader.GetTranslationData("English");
            try
            {
                Dictionary<string, string> table = new(english);
                Stats stats = new();

                // Should the game ever ship its own Russian, ours is laid over it rather than hiding it.
                TextAsset official = Resources.Load<TextAsset>("Translations/" + LanguageCode);
                if (official != null) Overlay(table, english, Parse(official.text), stats);

                Dictionary<string, string> russian = LoadTranslation(out string source);
                Overlay(table, english, russian, stats);

                Report(english, stats, source);
                return table;
            }
            catch (Exception ex)
            {
                RussianTranslationPlugin.Log.LogError($"Russian failed to load - showing English: {ex}");
                return english;
            }
        }

        /// <summary>
        /// The override file next to the DLL if there is one, read on every call so clicking
        /// Russian again in the menu picks up a translator's edits; else the embedded copy.
        /// </summary>
        private static Dictionary<string, string> LoadTranslation(out string source)
        {
            string path = Path.Combine(RussianTranslationPlugin.PluginDirectory, FileName);
            if (File.Exists(path))
            {
                try
                {
                    source = path;
                    return Parse(File.ReadAllText(path, Encoding.UTF8));
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    RussianTranslationPlugin.Log.LogError($"{path} can't be read - using the built-in translation. {ex.Message}");
                }
            }

            source = "built-in translation";
            return _embedded ??= Parse(ReadEmbedded());
        }

        private static string ReadEmbedded()
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FileName);
            if (stream == null) throw new InvalidOperationException($"embedded resource {FileName} is missing");

            using StreamReader reader = new(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static Dictionary<string, string> Parse(string json)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        private static void Overlay(Dictionary<string, string> table, Dictionary<string, string> english,
            Dictionary<string, string> russian, Stats stats)
        {
            foreach (KeyValuePair<string, string> pair in russian)
            {
                // Only keys the game still has: a stray CMDS_* key would become a bot command
                // (AssistanceBot.LoadCommandTranslations).
                if (!english.TryGetValue(pair.Key, out string source))
                {
                    stats.Obsolete.Add(pair.Key);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(pair.Value)) continue;

                table[pair.Key] = pair.Value;
                stats.Translated.Add(pair.Key);
                string? mismatch = ComparePlaceholders(source, pair.Value);
                if (mismatch != null) stats.Placeholders.Add($"{pair.Key}: {mismatch}");
            }
        }

        private static string? ComparePlaceholders(string english, string russian)
        {
            HashSet<string> expected = Placeholders(english);
            HashSet<string> found = Placeholders(russian);
            if (expected.SetEquals(found)) return null;

            List<string> parts = [];
            string[] missing = expected.Except(found).ToArray();
            string[] extra = found.Except(expected).ToArray();
            if (missing.Length > 0) parts.Add("missing " + string.Join(" ", missing));
            if (extra.Length > 0) parts.Add("not in English " + string.Join(" ", extra));
            return string.Join(", ", parts);
        }

        private static HashSet<string> Placeholders(string text)
        {
            HashSet<string> result = [];
            foreach (Match match in TagPattern.Matches(text))
            {
                if (!ColorTags.Contains(match.Groups[1].Value)) result.Add(match.Value);
            }
            return result;
        }

        private static void Report(Dictionary<string, string> english, Stats stats, string source)
        {
            List<string> missing = english.Keys.Where(k => !stats.Translated.Contains(k)).ToList();
            RussianTranslationPlugin.Log.LogInfo(
                $"Russian: {stats.Translated.Count}/{english.Count} strings translated " +
                $"({missing.Count} in English, {stats.Obsolete.Count} obsolete, " +
                $"{stats.Placeholders.Count} placeholder warnings) from {source}.");

            LogDetails("Shown in English", missing);
            LogDetails("Not in this game version, ignored", stats.Obsolete);
            LogDetails("Placeholder mismatch", stats.Placeholders);

            if (RussianTranslationPlugin.WriteReport) WriteUntranslated(english, missing);
        }

        private static void LogDetails(string title, List<string> items)
        {
            if (items.Count == 0) return;

            string shown = string.Join("; ", items.Take(DetailLimit));
            string more = items.Count > DetailLimit ? $"; ...and {items.Count - DetailLimit} more" : "";
            RussianTranslationPlugin.Log.LogWarning($"{title} ({items.Count}): {shown}{more}");
        }

        private static void WriteUntranslated(Dictionary<string, string> english, List<string> missing)
        {
            string path = Path.Combine(RussianTranslationPlugin.PluginDirectory, ReportFileName);
            try
            {
                Dictionary<string, string> report = missing.ToDictionary(k => k, k => english[k]);
                File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented), new UTF8Encoding(false));
                RussianTranslationPlugin.Log.LogInfo($"Wrote {missing.Count} untranslated strings to {path}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                RussianTranslationPlugin.Log.LogWarning($"Could not write {path}: {ex.Message}");
            }
        }
    }
}
