namespace Olden_Era___Template_Editor.Localization;

public static partial class Strings
{
    private static readonly (string Key, string Ru, string En)[] PatchOverrides =
    [
        ("S.Simple.Type.Team", "Командная игра", "Team game"),
        ("S.Simple.ExpNote", "⚠ Экспериментальный размер выше 256×256: размещение и производительность требуют проверки в игре.", "⚠ Experimental size above 256×256: placement and performance need in-game validation."),
        ("S.Simple.Help.Scale", "256×256 доступен как стандартный размер нового конфигуратора. Большие размеры до 400 в Simple требуют отдельной игровой проверки.", "256×256 is a standard size in the new game configurator. Larger Simple sizes up to 400 require separate game acceptance."),
        ("S.Chk.ExpMaps", "🗺 Большие карты: 272×272 … 512×512 (эксперимент.)", "🗺 Large maps: 272×272 … 512×512 (experimental)"),
        ("S.M.015", "Добавляет экспериментальные размеры выше 256×256 до 512×512. 256×256 доступен без этой отметки. Генерация и производительность больших карт требуют игровой проверки.", "Adds experimental sizes above 256×256 up to 512×512. 256×256 is available without this checkbox. Larger maps require game and performance acceptance."),
        ("S.CB.V.ExpSize", "Экспериментальный размер выше 256×256 требует проверки генерации и производительности в игре.", "Experimental sizes above 256×256 require in-game generation and performance acceptance.")
    ];
}
