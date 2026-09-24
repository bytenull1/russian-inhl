using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RussianTranslation
{
    /// <summary>
    /// Adds Russian to the game's language list by cloning one of its own language buttons.
    /// </summary>
    internal static class LanguageButton
    {
        // Buttons are named "LanguageButton <Language>". The Crowdin link is plain "LanguageButton";
        // the trailing space leaves it out.
        private const string NamePrefix = "LanguageButton ";
        private const string OwnName = NamePrefix + RussianStrings.LanguageCode;
        private const string PreferredTemplate = NamePrefix + "Ukrainian";
        // Other buttons show English names ("Ukrainian"), so this one does too.
        private const string Label = "Russian";

        private static bool _warnedNoTemplate;

        public static void Inject(LanguageSettingsMenu menu)
        {
            // An exception here would abort SettingsMenu.Init and leave the other settings menus uninitialized.
            try
            {
                Add(menu);
            }
            catch (Exception ex)
            {
                RussianTranslationPlugin.Log.LogError($"Could not add Russian to the language menu: {ex}");
            }
        }

        private static void Add(LanguageSettingsMenu menu)
        {
            TextButton[] buttons = menu.GetComponentsInChildren<TextButton>(true)
                .Where(b => b.name.StartsWith(NamePrefix, StringComparison.Ordinal))
                .ToArray();
            // Init runs twice in the game scene; a future official Russian button also ends up here.
            if (buttons.Any(b => b.name == OwnName)) return;

            TextButton? template = buttons.FirstOrDefault(b => b.name == PreferredTemplate);
            if (template == null) template = buttons.FirstOrDefault(b => b.gameObject.activeSelf);
            if (template == null)
            {
                if (!_warnedNoTemplate)
                {
                    RussianTranslationPlugin.Log.LogWarning(
                        "No language button to copy - Russian can't be picked in the menu. " +
                        "A saved Russian setting still loads.");
                    _warnedNoTemplate = true;
                }
                return;
            }

            Transform list = template.transform.parent;
            GameObject clone = Object.Instantiate(template.gameObject, list, false);
            clone.name = OwnName;
            clone.SetActive(true);

            // The game skips disabled Translation components (SceneLoader.LoadCurrentTranslationData),
            // so none can overwrite the label.
            foreach (Translation translation in clone.GetComponentsInChildren<Translation>(true))
            {
                translation.enabled = false;
            }

            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = Label;

            // The copied persistent call is SelectLanguage("Ukrainian"); switch it off.
            UnityEvent onClick = clone.GetComponent<TextButton>().OnClick;
            for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
            {
                onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            onClick.AddListener(() => menu.SelectLanguage(RussianStrings.LanguageCode));

            clone.transform.SetSiblingIndex(AlphabeticalIndex(list, clone.transform));
            if (list is RectTransform rect) LayoutRebuilder.MarkLayoutForRebuild(rect);

            RussianTranslationPlugin.Log.LogInfo($"Added Russian to the language menu ({menu.gameObject.scene.name}).");
        }

        /// <summary>
        /// The list is alphabetical: the slot of the first language button that sorts after ours.
        /// </summary>
        private static int AlphabeticalIndex(Transform list, Transform own)
        {
            foreach (Transform sibling in list)
            {
                if (sibling == own || !sibling.name.StartsWith(NamePrefix, StringComparison.Ordinal)) continue;
                if (string.CompareOrdinal(sibling.name, OwnName) > 0) return sibling.GetSiblingIndex();
            }
            return list.childCount - 1;
        }
    }
}
