using System.Collections.Generic;
using System.ComponentModel;
using HarmonyLib;
// ReSharper disable InconsistentNaming - this breaks Harmony patches.

namespace RussianTranslation
{
    /// <summary>
    /// Harmony patches, one nested class per feature so a game update that breaks one target
    /// disables only that feature (RussianTranslationPlugin.ApplyPatches).
    /// </summary>
    public static class Patches
    {
        [HarmonyPatch, Description("the Russian strings")]
        internal static class Strings
        {
            /// <summary>
            /// The game's one step from a language code to its string table. Russian is served
            /// here; every other code runs the original.
            /// </summary>
            [HarmonyPatch(typeof(ResourceLoader), nameof(ResourceLoader.GetTranslationData))]
            [HarmonyPrefix]
            public static bool ResourceLoader_GetTranslationData_Prefix(string index, ref Dictionary<string, string> __result)
            {
                if (index != RussianStrings.LanguageCode) return true;

                __result = RussianStrings.Build();
                return false;
            }
        }

        [HarmonyPatch, Description("the Russian button in the language menu")]
        internal static class Button
        {
            /// <summary>
            /// Runs once per scene (main menu, game) when the settings menus initialize.
            /// </summary>
            [HarmonyPatch(typeof(LanguageSettingsMenu), nameof(LanguageSettingsMenu.Init))]
            [HarmonyPostfix]
            public static void LanguageSettingsMenu_Init_Postfix(LanguageSettingsMenu __instance)
            {
                LanguageButton.Inject(__instance);
            }
        }
    }
}
