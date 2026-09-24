using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace RussianTranslation
{
    /// <summary>
    /// Adds Russian to Isolated Inhale's language menu. Strings the translation lacks stay in
    /// English. No game files are replaced. See README.md.
    /// </summary>
    [BepInPlugin(Guid, "Russian Translation", "1.0.0")]
    [BepInProcess("Isolated Inhale.exe")]
    public sealed class RussianTranslationPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.bytenull1.russiantranslation";

        private static ManualLogSource? _log;
        /// <summary>
        /// The plugin's logger; anything logging before Awake gets a stand-in source.
        /// </summary>
        public static ManualLogSource Log => _log ??= BepInEx.Logging.Logger.CreateLogSource("RussianTranslation");

        /// <summary>
        /// The folder holding the DLL. A Russian.json there overrides the embedded translation.
        /// </summary>
        public static string PluginDirectory { get; private set; } = Paths.PluginPath;

        private static ConfigEntry<bool>? _writeReport;
        public static bool WriteReport => _writeReport != null && _writeReport.Value;

        private void Awake()
        {
            _log = Logger;
            PluginDirectory = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;
            _writeReport = Config.Bind("General", "WriteReport", false,
                "Each time Russian loads, write untranslated.json next to the plugin: every string that " +
                "still shows in English, with its English text. For translators after a game update.");
            ApplyPatches();
        }

        /// <summary>
        /// Applies each patch group on its own, so a game update that removes one target
        /// disables that one feature, named in the log, instead of every patch after it.
        /// </summary>
        private void ApplyPatches()
        {
            Harmony harmony = new(Guid);
            int applied = 0, total = 0;
            foreach (Type group in typeof(Patches).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (!group.IsDefined(typeof(HarmonyPatch), false)) continue;

                total++;
                try
                {
                    harmony.CreateClassProcessor(group).Patch();
                    applied++;
                }
                catch (Exception ex)
                {
                    string feature = group.GetCustomAttribute<DescriptionAttribute>()?.Description ?? group.Name;
                    Logger.LogError($"Patch group '{group.Name}' failed - {feature} disabled: {ex}");
                }
            }

            if (applied == total) Logger.LogInfo($"All {total} patch groups applied.");
            else Logger.LogWarning($"{applied} of {total} patch groups applied - see errors above.");
        }
    }
}
