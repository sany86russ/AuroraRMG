namespace Olden_Era___Template_Editor.Localization;

public static partial class Strings
{
    private static readonly (string Key, string Ru, string En)[] PatchWorkflowLabels =
    [
        ("S.Patch.DiagnosticOmissions", "В диагностике {0} полей нативных схем заменены размером и SHA256. Исходные настройки сохраняют полный текст.", "In diagnostics, {0} native schema fields were replaced by byte counts and SHA256 fingerprints. Original settings retain their full text."),
        ("S.Patch.SlotLocked", "Закрепить", "Locked"),
        ("S.Patch.BalanceTreasure", "Охраняемое наполнение: минимум {0} переходов; минимальный бюджет охраны пути: {1}.", "Guarded content: minimum {0} hops; minimum path guard budget: {1}."),
        ("S.Patch.BalancePortal", "Портал {0}: {1} → {2}; объект со стороны from: {3}; объект со стороны to: {4}; охрана: {5}.", "Portal {0}: {1} → {2}; from-side object: {3}; to-side object: {4}; guard budget: {5}."),
        ("S.Patch.BalancePortalHelp", "Флаги показывают настроенные объекты на концах соединения. Отчёт не определяет по ним односторонность перемещения в игре. Минимумы переходов и охраны могут относиться к разным путям.", "Flags describe the configured endpoint objects. The report does not infer one-way in-game travel from them. Minimum hops and guard budgets can belong to different paths."),
        ("S.Patch.CurrentPresets", "Пресеты: профиль 0.81.04", "Presets: 0.81.04 profile"),
        ("S.Patch.TeamPresets", "Пресеты: команды и кооператив", "Presets: teams and co-op"),
        ("S.Patch.LegacyPresets", "Прежние пресеты Aurora 1.11", "Legacy Aurora 1.11 presets"),
        ("S.Bal.Find.AreaBudgets", "Сравниваются коэффициенты плотности. Итоговые бюджеты зависят от пригодной площади зон внутри игры.", "Density coefficients are compared. Final budgets depend on eligible zone area inside the game."),
        ("S.Patch.BalanceDensity", "Сходство коэффициентов плотности: {0}/100", "Density coefficient similarity: {0}/100"),
        ("S.Patch.BalanceTeams", "Сходство команд: {0}/100", "Team similarity: {0}/100"),
        ("S.Patch.AreaTotals", "Коэффициенты на площадь: сокровища {0}, ресурсы {1}. Итоговая площадь определяется игрой.", "Per-area coefficients: treasure {0}, resources {1}. Eligible area is determined by the game."),
        ("S.Patch.BatchProgress", "Обработано {0} из {1}. Seed: {2}", "Processed {0} of {1}. Seed: {2}"),
        ("S.Patch.BatchResult", "Готово: {0}. Ошибок: {1}. Отмена: {2}.", "Succeeded: {0}. Failed: {1}. Cancelled: {2}."),
        ("S.Patch.BatchCurrentPresets", "Набор из 43 новых пресетов", "Batch of 43 patch presets"),
        ("S.Patch.BatchTeamPresets", "Набор командных пресетов", "Team preset batch"),
        ("S.Patch.BatchLimit", "Один набор может содержать до {0} шаблонов. Уменьшите число seeds.", "One batch can contain up to {0} templates. Reduce the number of seeds."),
        ("S.Patch.OptionalPreview", "Добавить схему зон в архив? Она раскрывает структуру карты.", "Include the zone diagram in the archive? It reveals the map structure."),
        ("S.Patch.OptionalSettings", "Добавить полные настройки для повторения шаблона? Они могут содержать авторские и импортированные данные. Перед передачей архива проверьте содержимое.", "Include complete settings to reproduce the template? They may contain custom and imported data. Review the contents before sharing the archive."),
        ("S.Patch.PackageSensitiveSettings", "Настройки содержат поля учётных данных. Экспортируйте пакет без настроек или удалите эти поля из отдельной копии; исходные настройки не изменены.", "Settings contain credential fields. Export without settings or remove those fields from a separate copy; the original settings were not changed."),
        ("S.Patch.OptionalLogs", "Добавить выбранные вами логи? Можно продолжить без них. В архив попадут только ограниченные фрагменты с удалением приватных данных.", "Include logs you select? You can continue without them. Only limited, redacted excerpts will be included."),
        ("S.Patch.Cancelled", "Операция отменена", "Operation cancelled"),
        ("S.Patch.TwoPlayers", "Поддержанный экспорт турнира и финальной битвы требует двух отдельных противников. Для команд выберите другое условие победы.", "Supported tournament and final-battle exports require two individual opposing players. Select another victory condition for teams."),
        ("S.Patch.DiagnosticReceipt", "Удалено приватных фрагментов: {0}. Сокращено логов: {1}. Ошибок чтения: {2}.", "Private fragments removed: {0}. Logs truncated: {1}. Read errors: {2}."),
        ("S.Patch.InvalidEditor", "Исправьте некорректные значения в текущих полях редактора.", "Correct the invalid values in the current editor fields."),
        ("S.Patch.BulkTitle", "Массовая правка зон", "Bulk zone edits"),
        ("S.Patch.Field.GameSeed", "Seed игры (для памятки, hex)", "Game seed (for setup notes, hex)"),
        ("S.Patch.Field.RandomizePlayerAssignment", "Случайно распределять игроков по командам", "Randomize player assignments to teams"),
        ("S.Patch.BulkHint", "Выберите вариант, свойство и зоны. Значение вводится в JSON: число, массив, true/false или null для удаления свойства. Правка применяется к выбранным зонам целиком и отменяется одним Undo. Остальные зоны и варианты сохраняются.", "Select a variant, property and zones. Enter a JSON value: number, array, true/false or null to remove the property. The edit commits atomically and can be reverted with one Undo. Other zones and variants are preserved."),
        ("S.Patch.BalanceTitle", "Подробный отчёт о структуре и балансе", "Detailed structure and balance report"),
        ("S.Patch.BalancePlayer", "{0}, {1}: города {2}; стартовые ресурсы {3}; коэффициент площади {4}; до противника {5} зон; охрана на пути к городу {6}.", "{0}, {1}: towns {2}; starting resources {3}; area coefficient {4}; enemy distance {5} hops; town access guard {6}."),
        ("S.Patch.BalanceTeam", "{0}: игроки {1}; города {2}; ресурсы {3}; коэффициент площади {4}; до противника {5} зон.", "{0}: players {1}; towns {2}; resources {3}; area coefficient {4}; enemy distance {5} hops."),
        ("S.Patch.ImportWarning", "Настройки перенесены в Advanced. Aurora создаёт собственную структуру карты.\n{0}", "Settings were imported into Advanced. Aurora generates its own map structure.\n{0}"),
        ("S.Patch.Card.Summary", "Игроки: {0}; альянсы: {1}. Размер: {2} × {3}. Победа: {4}. Герои: {5}–{6}. Богатство: {7}; опасность карты: {8}; темп: {9}.", "Players: {0}; alliances: {1}. Size: {2} × {3}. Victory: {4}. Heroes: {5}–{6}. Richness: {7}; map threat: {8}; pace: {9}."),
        ("S.Patch.Card.Provenance", "Seed: {0}; профиль: {1}; алгоритм: {2}.", "Seed: {0}; profile: {1}; algorithm: {2}."),
        ("S.Patch.Card.Tournament", "Даты турнирных боёв: {0}. Объявления: {1}. Сохранять армию: {2}.", "Tournament battle days: {0}. Announcements: {1}. Save army: {2}."),
        ("S.Patch.Card.Final", "Арена: задержка регистрации {0}; длительность {1}. Профиль: {2}.", "Arena: registration delay {0}; duration {1}. Profile: {2}."),
        ("S.Patch.Card.Tuning", "Плотность: ресурсы {0}%, объекты {1}%; сила нейтралов {2}%, границ {3}%.", "Density: resources {0}%, objects {1}%; strength: neutrals {2}%, borders {3}%."),
        ("S.Patch.Card.Desertion", "Поражение без владений: с дня {0}, порог армии {1}.", "Defeat without holdings: from day {0}, army threshold {1}."),
        ("S.Patch.Card.FinalDay", "Выбранный день финального боя: {0}.", "Selected final battle day: {0}."),
        ("S.Patch.PoolWeightHelp", "Вес задаёт относительную вероятность, не количество. Лимиты и обязательность настраиваются отдельно. Вариант -1 сохраняет исходное значение native по умолчанию; 0 и выше — индекс конфигурации наград. Пустое поле сохраняет отсутствие варианта.", "Weight controls relative probability, not quantity. Limits and mandatory content are configured separately. Variant -1 preserves the native default; 0 and above are configured reward indices. A blank field preserves an omitted variant."),
        ("S.Patch.PoolGroups", "Группы выбора", "Selection groups"),
        ("S.Patch.PoolGroup", "Группа", "Group"),
        ("S.Patch.PoolRemoveList", "Убрать список", "Remove list"),
        ("S.Patch.PoolCatalogAdd", "Добавить из каталога", "Add from catalog"),
        ("S.Patch.PoolManualAdd", "Добавить SID вручную", "Add SID manually"),
        ("S.Patch.PoolRemoveObject", "Убрать объект", "Remove object"),
        ("S.Patch.PoolEmbedLists", "Встроить определения выбранных списков в шаблон", "Embed selected list definitions in template"),
        ("S.Patch.PoolObjectOnly", "Для пула выберите объект карты.", "Select a map object for the pool."),
        ("S.Patch.PoolError.Name", "Укажите допустимое имя пула.", "Enter a valid pool name."),
        ("S.Patch.PoolError.NullGroup", "Группа выбора отсутствует.", "A selection group is missing."),
        ("S.Patch.PoolError.NullItem", "Элемент содержимого отсутствует.", "A content item is missing."),
        ("S.Patch.PoolError.ListName", "Укажите имя списка содержимого.", "Enter a content list name."),
        ("S.Patch.PoolError.DuplicateList", "Один список добавлен несколько раз.", "A list was added more than once."),
        ("S.Patch.PoolError.Sid", "Укажите SID объекта.", "Enter an object SID."),
        ("S.Patch.PoolError.Variant", "Вариант должен быть целым числом -1 или больше.", "Variant must be an integer of -1 or greater."),
        ("S.Patch.PoolError.UnknownVariant", "Для {0} не найден вариант {1} в текущем каталоге.", "Variant {1} was not found for {0} in the current catalog."),
        ("S.Patch.PoolError.DuplicateItem", "Этот объект и вариант уже добавлены.", "This object and variant were already added."),
        ("S.Patch.PoolError.EmptyGroup", "Группа выбора не содержит объектов или списков.", "The selection group contains no objects or lists."),
        ("S.Patch.PoolError.ZeroGroups", "Нужна хотя бы одна группа с положительным весом.", "At least one group must have a positive weight."),
        ("S.Patch.PoolError.Weight", "Вес должен быть конечным неотрицательным числом.", "Weight must be finite and non-negative."),
        ("S.Patch.PoolError.Copy", "Не удалось скопировать определение пула.", "Could not copy the pool definition."),
        ("S.Patch.PoolError.DuplicateName", "Пул с этим именем уже существует.", "A pool with this name already exists.")
        ,("S.PV.ColGroupWeight", "Вес группы", "Group weight")
        ,("S.PV.ColVariant", "Вариант", "Variant")
        ,("S.PV.ColMissing", "Не найдено", "Unresolved")
        ,("S.PV.ColGroupShare", "Доля группы", "Group share")
        ,("S.PV.ColListShare", "Доля в списке", "Within-list share")
        ,("S.PV.ColGroup", "Группа", "Group")
        ,("S.PV.WeightHint", "Доли описывают настроенные веса внутри группы/списка; они не гарантируют число объектов или итоговый шанс размещения.", "Shares describe configured group/list weights; they do not guarantee object counts or final placement probability.")
        ,("S.PV.GroupSummary", "Группа {0}: вес {1}; прямых объектов {2}; списки: {3}", "Group {0}: weight {1}; direct items {2}; lists: {3}")
        ,("S.PV.Origin.TemplateDefinition", "Определение из шаблона", "Template definition")
        ,("S.PV.Origin.InstalledGame", "Установленная игра", "Installed game")
        ,("S.PV.Origin.LocalDefinition", "Локальное определение", "Local definition")
        ,("S.PV.Origin.Missing", "Определение не найдено", "Definition missing")
        ,("S.PV.Origin.GameUnavailable", "Данные игры недоступны", "Game data unavailable")
        ,("S.PV.Origin.InvalidTemplateDefinition", "Некорректное определение из шаблона", "Invalid template definition")
        ,("S.PV.DependenciesEmpty", "Нет зависимостей", "No dependencies")
        ,("S.Patch.AiHelp", "SID профилей взяты из установленной игры. Их поведение в авторских картах ещё требует игровой проверки. Тип людей/ИИ и сложность ИИ задаются в lobby.", "Profile SIDs are read from the installed game. Their behavior in custom maps still needs game acceptance. Human/AI slots and AI difficulty are selected in the lobby.")
    ];
}
