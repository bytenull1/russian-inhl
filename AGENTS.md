# AGENTS.md - rules for AI agents in this repository

**Russian Translation** is a BepInEx 5 plugin for *Isolated Inhale* v0.8.9 (Unity 2022.1.20f1). It adds
Russian to the game's language menu. Player and translator docs: [README.md](README.md).

## Where things are

| File | Role |
| --- | --- |
| `RussianTranslation/Patches.cs` | The only two Harmony patches: `ResourceLoader.GetTranslationData` (strings) and `LanguageSettingsMenu.Init` (button). |
| `RussianTranslation/RussianStrings.cs` | English + Russian overlay, validation, log summary, `untranslated.json`. |
| `RussianTranslation/LanguageButton.cs` | Clones a language button for Russian. |
| `RussianTranslation/Resources/Russian.json` | The translation, embedded in the DLL. |
| `tools/check_translation.py` | The translation linter. CI runs it without `--english`. |

## Rules

- **Game text goes through `SceneLoader.GetTranslatedText`.** A missing key shows `NO_DATA`. That is
  why Russian is always English plus an overlay, never a standalone table.
- **Only keys English has reach the table.** `AssistanceBot.LoadCommandTranslations` turns every
  `CMDS_*` key into a bot command, so a stale key would become a phantom command.
- **Never break the game.** Patch code catches its own exceptions, logs them and degrades to English,
  or to no button.
- **Language code `Russian`.** It is saved in the game's settings. The game resolves an unknown code to
  English, which is what makes uninstalling safe. Don't change the code.

## Code conventions

- Comments are pointers, not essays: at most two or three lines. No capitals for emphasis.
- Nullable reference types are on and warnings are errors. Each `!` carries a one-line comment naming the
  guard the compiler can't see.
- Unity objects: compare with `== null`, not `?.`, `??` or `is null` (the analyzers enforce this).

**Build:** `dotnet build RussianTranslation/RussianTranslation.csproj -c Release` (and `-c Debug`). Both must
build with no warnings. Then run `python tools/check_translation.py --english English.json` if you have the
English locally, and without `--english` if you don't.

## Docs

Docs describe the code as it is now. Keep verification status, dates and test plans out of committed
files, put them in your reply.
