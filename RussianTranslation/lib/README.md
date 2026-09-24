# Библиотеки для сборки

Положите сюда DLL, вместе с которыми компилируется проект. Они **не** распространяются вместе с
модом (`*.dll` в этой папке в .gitignore), скопируйте их из своей установленной игры.

Проект ссылается на них через `HintPath` в эту папку с `Private=false`, поэтому они нужны только
для компиляции и не копируются в результат сборки. Во время игры их и так предоставляет BepInEx и
сама игра.

Из `BepInEx/core/`:

- `BepInEx.dll`
- `0Harmony.dll`

Из `Isolated Inhale_Data/Managed/`:

- `Assembly-CSharp.dll`
- `Newtonsoft.Json.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.UI.dll`
- `Unity.TextMeshPro.dll`
