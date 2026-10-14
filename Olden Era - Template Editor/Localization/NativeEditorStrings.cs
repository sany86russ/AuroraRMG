namespace Olden_Era___Template_Editor.Localization;

public static partial class Strings
{
    private static readonly (string Key, string Ru, string En)[] NativeEditorLabels =
    [
        ("S.NativeEditor.Title", "Схемы нативного генератора", "Native generator schemas"),
        ("S.NativeEditor.Notice", "Редактирование .mrmg доступно офлайн. Выбор пользовательской схемы в меню игры пока не подтверждён. Сохраняйте копию вне папки игры.", "Offline .mrmg editing is available. Selecting a custom schema in the game menu is not yet confirmed. Save a copy outside the game folder."),
        ("S.NativeEditor.Open", "Открыть .mrmg", "Open .mrmg"),
        ("S.NativeEditor.SaveAs", "Сохранить копию", "Save a copy"),
        ("S.NativeEditor.Apply", "Применить правку", "Apply edit"),
        ("S.NativeEditor.RevertEdit", "Отменить правку", "Revert edit"),
        ("S.NativeEditor.NoFile", "Откройте .mrmg профиль для просмотра и редактирования.", "Open an .mrmg profile to inspect and edit it."),
        ("S.NativeEditor.ConditionalTab", "Условный параметр", "Conditional parameter"),
        ("S.NativeEditor.RawTab", "Исходный JSON", "Raw JSON"),
        ("S.NativeEditor.ConditionsAnd", "Все условия (AND)", "All conditions (AND)"),
        ("S.NativeEditor.ConditionsOr", "Любое условие (OR)", "Any condition (OR)"),
        ("S.NativeEditor.Weight", "Вес выбора", "Selection weight"),
        ("S.NativeEditor.Fallback", "Запасной вариант", "Fallback row"),
        ("S.NativeEditor.Value", "Значение (JSON)", "Value (JSON)"),
        ("S.NativeEditor.Root", "Схема", "Schema"),
        ("S.NativeEditor.FileFilter", "Профили генератора (*.mrmg.json)|*.mrmg.json|JSON (*.json)|*.json", "Generator profiles (*.mrmg.json)|*.mrmg.json|JSON (*.json)|*.json"),
        ("S.NativeEditor.TooLarge", "Файл превышает лимит редактора 4 МБ.", "The file exceeds the editor's 4 MB limit."),
        ("S.NativeEditor.Loaded", "Открыто: {0}", "Opened: {0}"),
        ("S.NativeEditor.Modified", "Изменения применены. Сохраните отдельную копию.", "Edits applied. Save a separate copy."),
        ("S.NativeEditor.Saved", "Копия сохранена: {0}", "Copy saved: {0}"),
        ("S.NativeEditor.Unapplied", "Примените или отмените текущую правку перед переходом к другому узлу или разделу.", "Apply or revert the current edit before switching to another node or section."),
        ("S.NativeEditor.InvalidTarget", "Выбранный узел больше недоступен.", "The selected node is no longer available."),
        ("S.NativeEditor.ConditionsObject", "Условия должны быть JSON объектом.", "Conditions must be a JSON object."),
        ("S.NativeEditor.InvalidWeight", "Укажите неотрицательный вес в диапазоне native float (до 3.4e38); дробную часть вводите через точку.", "Enter a non-negative weight representable as a native float (up to 3.4e38), using a decimal point."),
        ("S.NativeEditor.ReferenceErrors", "Исправьте ссылки перед сохранением:\n{0}", "Fix references before saving:\n{0}"),
        ("S.NativeEditor.GameFolderBlocked", "Сохранение в папку игры запрещено. Выберите отдельную папку для копии.", "Saving into the game folder is blocked. Choose a separate folder for the copy."),
        ("S.NativeEditor.Discard", "Закрыть несохранённые изменения?", "Discard unsaved edits?"),
        ("S.NativeEditor.Error", "Ошибка: {0}", "Error: {0}")
    ];
}
