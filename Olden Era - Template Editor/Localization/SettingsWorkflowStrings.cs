namespace Olden_Era___Template_Editor.Localization;

public static partial class Strings
{
    private static readonly (string Key, string Ru, string En)[] SettingsWorkflowLabels =
    [
        ("S.Vov.Catalog", "Выбрать из каталога игры…", "Choose from the game catalog…"),
        ("S.Vov.CatalogHelp", "Выберите объект и строку награды, чтобы задать охрану для этого варианта. Если строку не выбрать, значение применяется ко всем вариантам объекта.", "Choose an object and a reward row to set a guard value for that variant. Leave the reward row unselected to apply it to every variant of the object."),
        ("S.Vov.ExistingTarget", "Для {0} уже указана сила охраны.", "{0} already has a guard override."),
        ("S.Settings.UnsavedTitle", "Несохранённые настройки", "Unsaved settings"),
        ("S.Settings.SaveDocument", "Сохранить изменения настроек «{1}» в режиме «{0}»? «Нет» продолжит без сохранения.", "Save settings changes for '{1}' in {0} mode? Choose No to discard the changes."),
        ("S.Settings.SaveTemplateBeforeClosing", "Сохранить изменения шаблона «{0}»? «Нет» закроет шаблон без сохранения.", "Save changes to the template '{0}'? Choose No to close it without saving."),
        ("S.Settings.NullContentRow", "В файле настроек есть пустая запись дополнительного контента.", "The settings file contains an empty extra-content row."),
        ("S.Settings.PreviewFromSimple", "Этот шаблон создан в простом режиме. Чтобы работать с ним здесь, откройте его настройки из простого режима или создайте новый шаблон.", "This template was generated in Simple mode. To work with it here, open its settings from Simple mode or generate a new template."),
        ("S.Settings.OpenEmptyEditor", "Предпросмотр не соответствует текущим настройкам. Открыть пустой редактор для загрузки шаблона из файла?", "The preview does not match these settings. Open an empty editor to load a template from a file?")
    ];
}
