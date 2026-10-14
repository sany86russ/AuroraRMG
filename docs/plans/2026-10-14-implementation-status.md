# Выполнение плана обновления AuroraRMG

Все 54 задачи отслеживаются по исходным критериям. Реализация, автоматическая проверка и проверка в игре фиксируются отдельно. Релиз требует отдельного согласования владельца проекта.

48 задач реализации выполнены и проверены офлайн. По шести исследовательским направлениям реализованы подтверждённые механизмы и зафиксированы границы движка. Неподтверждённые возможности не подменяются выдуманными полями или режимами.

| ID | Задача | Статус | Доказательства |
|---|---|---|---|
| P0-01 | Профиль установленной игры: путь, build, версия данных, поддержанные параметры; отдельно старое поведение Aurora и профиль 0.81.04 | Реализовано; проверено офлайн | GameInstallationService, GameProfileTests: версия/build, fingerprints, Legacy/Current и предупреждение неизвестного снимка |
| P0-02 | Типизировать команды, описание и подтверждённые объектные поля; заменить целые веса реакции на дробные | Реализовано; проверено офлайн | PatchSchemaCompatibilityTests: typed spawnInfos/descBlocks, nullable объектные поля, дробные native float-веса |
| P0-03 | Устранить сохранение устаревшего результата в Simple и закрыть пропуски Advanced | Реализовано; проверено офлайн | MainWindow.PatchHarness: stale Simple/Advanced блокирует экспорт; имя обновляет результат и снимки |
| P0-04 | Использовать реальное значение `TournamentRules.SaveArmy` и раскрыть поддержанную настройку | Реализовано; проверено офлайн | GenerationPatchProfileTests: фактический SaveArmy; календарь и отдельный флаг в UI |
| P0-05 | Версионировать настройки и перейти с индексов списков на устойчивые IDs | Реализовано; проверено офлайн | SettingsMigrationTests, SettingsUnknownFieldTests: schema v2, устойчивые IDs, future unknown без потерь |
| P0-06 | Проверять полный шаблон и согласованность variants | Реализовано; проверено офлайн | TemplateCompatibilityValidator, TemplateJsonReaderTests: все variants, правила/refs/индексы наград, отказ от duplicate JSON |
| P0-07 | Единый поиск игры для каталога и экспорта | Реализовано; проверено офлайн | GameInstallationTests, ExportDestinationTests: общий выбранный install, вторичные библиотеки и ручной путь |
| P0-08 | Обновление каталога и проверка старых ссылок | Реализовано; проверено офлайн | GameCatalogRefreshTests: актуальный cache, изменение Core, полный refs snapshot; removed SID без выдуманного remap |
| A-01 | Дизайнер команд: игрок → альянс, FFA, случайное или ручное распределение | Реализовано; проверено офлайн | AlliancePlan + UI harness: точные FFA/ручные альянсы, seeded shuffle, индивидуальные locks |
| A-02 | Неравные и нечётные составы | Реализовано; проверено офлайн | GenerationPatchProfileTests: нечётные/неравные составы; 5 игроков и 2v2v1 сохраняются |
| A-03 | Выбор разрешения изменения команд в lobby | Реализовано; проверено офлайн | GenerationOptionsWindow + UI harness: общий флаг и закрепление каждого ручного слота |
| A-04 | Командные старты и общие тылы | Реализовано; проверено офлайн | TeamLayoutBuilder: территории союзников, общие тылы, реальные отдельные старты |
| A-05 | Командные коридоры, фронт и общая арена | Реализовано; проверено офлайн | GenerationPatchProfileTests: corridors/territories/common arena, buffer depth и симметричные portal links |
| A-06 | Баланс по командам и по отдельным игрокам | Реализовано; проверено офлайн | TemplateBalanceReport: player/team метрики и помощь меньшей команде, без обещания одинаковой геометрии |
| A-07 | Набор кооперативных пресетов | Реализовано; проверено офлайн | PatchPresetTests, acceptance suite: 10 новых командных/кооперативных presets |
| A-08 | Настройка матча после экспорта | Реализовано; проверено офлайн | Карточка и RU/EN package/manual kit: тип человека/ИИ, фракция и сложность выбираются в lobby |
| B-01 | Самостоятельный режим «Падение стража» | Реализовано; проверено офлайн | GenerationPatchProfileTests: Guardian’s Demise ID 8 и ключевой стартовый герой |
| B-02 | Ключевые старты и цели удержания в редакторе | Реализовано; проверено офлайн | TemplatePropertiesDraftTests, editor UI: KeyObject и цели удержания сохраняются отдельными полями |
| B-03 | Единая модель условий и разрешённых сочетаний | Реализовано; проверено офлайн | PatchSchemaCompatibilityTests: завершение победы, совместимые flags и отказ от неподтверждённых сочетаний |
| B-04 | Календарь турнира | Реализовано; проверено офлайн | BattleCalendar + tests: возрастание дат, native zero-based ↔ человеческие дни |
| B-05 | Настраиваемое окно объявления и последовательные бои | Реализовано; проверено офлайн | GenerationPatchProfileTests: announcement lead, последовательные/соседние бои и SaveArmy |
| B-06 | Понятная дата финального боя и способ запуска | Реализовано; проверено офлайн | GenerationPatchProfileTests: выбранный день финала, регистрация, длительность и Legacy schedule отдельно |
| B-07 | Лимиты героев и найм по профилям | Реализовано; проверено офлайн | GenerationPatchProfileTests: вместимость найма, прирост за города, caps и невозможная арифметика; actual spawn pending game |
| B-08 | Удержание города и поведение таймера | Реализовано; проверено офлайн | GenerationPatchProfileTests: отмеченная цель, hold days и nullable reset timer |
| C-01 | Пять уровней богатства с отдельными настройками ресурсов и объектов | Реализовано; проверено офлайн | GenerationPatchProfileTests + acceptance: 5 verified richness tables; ресурсы/объекты имеют отдельные бюджеты |
| C-02 | Развести сложность генерации, нейтральные армии, границы и сложность ИИ | Реализовано; проверено офлайн | GenerationPatchProfileTests: 6 уровней угрозы; экономика и combat fingerprint независимы, AI difficulty в lobby |
| C-03 | Региональная прогрессия | Реализовано; проверено офлайн | GenerationProfileApplier: богатство/охрана по ролям зон и native progression tables |
| C-04 | Бюджеты по площади и ограничение перегруженных зон | Реализовано; проверено офлайн | NativeSchemaTests/AreaContentSummaryTests: coefficients × реальные свободные клетки, overflow/round-to-even; площадь не угадывается |
| C-05 | Число городов независимо от богатства | Реализовано; проверено офлайн | GenerationPatchProfileTests: neutral town total и cap независимы от богатства |
| C-06 | Размещение городов и аванпостов | Реализовано; проверено офлайн | GenerationPatchProfileTests: role-based towns, common territories и отдельные starting towns |
| C-07 | Симметрия условий и команд | Реализовано; проверено офлайн | GenerationPatchProfileTests: равные стартовые условия, content/size/guard symmetry, odd town remainder |
| C-08 | Настройки темпа и стартовой угрозы | Реализовано; проверено офлайн | GenerationPatchProfileTests: темп, стартовая экономика/охрана и buffer; источник настроек не мутируется |
| D-01 | Актуальный каталог объектов с RU/EN именами и SID | Реализовано; проверено офлайн | GameCatalogTests/RefreshTests + UI: RU/EN, SID, категории/уровни, RMG membership отдельно от placement proof |
| D-02 | Параметры наград и уровней свитков | Реализовано; проверено офлайн | ContentPoolComposerTests/PatchSchemaCompatibilityTests: фактические reward indices и spell variants; null/−1 сохранены |
| D-03 | Вес, лимит и обязательность содержимого | Реализовано; проверено офлайн | ContentPoolComposerTests/PoolContentInspectorTests: дробные weights, limits/mandatory отдельно; omitted weight=1 и explicit zero |
| D-04 | Реки и расстояние между притоками | Реализовано; проверено офлайн | GenerationPatchProfileTests/TemplatePropertiesDraftTests: nullable rivers и расстояние между притоками всех variants |
| D-05 | Недельный рост найма и стартовые запасы | Реализовано; проверено офлайн | GenerationPatchProfileTests: семь tier arrays, weekly/first week flags и initial increments |
| D-06 | Особые правила магии и развития | Реализовано; проверено офлайн | GenerationPatchProfileTests: faction laws, guild и custom learning — отдельные verified switches |
| D-07 | Маршруты и порталовые настройки | Реализовано; проверено офлайн | PortalPlacementRefactorTests: endpoint objects/placement references; flags не названы гарантией одностороннего travel |
| D-08 | Массовая правка и безопасные структурные операции | Реализовано; проверено офлайн | TemplateBulkEditorTests/PortalPlacementRefactorTests: transactional batch, undo, rename/delete/mirror refs и unknown preservation |
| E-01 | Современная игровая карточка шаблона | Реализовано; проверено офлайн | TemplateCardBuilder + tests: effective rules/hero caps/teams, author text и provenance |
| E-02 | Реальные теги и фильтры | Реализовано; проверено офлайн | Карточка и package tests: теги по фактическому экспорту, а не по старому выбранному label |
| E-03 | Пересмотр 43 готовых пресетов и обычный размер 256 | Реализовано; проверено офлайн | PatchPresetTests + acceptance: 43 Current presets, отдельные 43 Legacy; стандартный 256 и эксперимент выше |
| E-04 | Расширенный отчёт баланса | Реализовано; проверено офлайн | GenerationPatchProfileTests: starts/cities/resources/coefficients, независимые min hops/min guards и portal flags |
| E-05 | Повторяемость шаблона и независимый seed игры | Реализовано; проверено офлайн | SettingsSnapshotMapperTests + UI: exact actual input, independent game seed, неизменяемые копии и повторяемый JSON |
| E-06 | Экспорт и обмен пакетом | Реализовано; проверено офлайн | TemplatePackageTests/ExportDestinationTests: атомарный ZIP, hashes/profiles, RU/EN guide и защита exact settings credentials |
| E-07 | Пакетная генерация и проверка | Реализовано; проверено офлайн | TemplateBatchTests: immutable input, sequential seeds, max128, cancellation/ошибки/overwrite как явные receipts |
| E-08 | Минимальный пакет диагностики | Реализовано; проверено офлайн | DiagnosticBundleTests: только выбранные bounded logs, redact/private paths, native schema size+SHA omission receipt |
| R-01 | Чтение и редактирование `.mrmg` профилей | Исследование завершено; подтверждённая часть реализована | NativeSchemaTests + native UI: offline reader/editor/condition evaluator и safe copy; регистрация в game picker не доказана |
| R-02 | Импорт native конфигурации или результата генерации | Исследование завершено; подтверждённая часть реализована | NativeConfigurationImportTests: JSON/debug/compact codec, supported inputs/raw unknown; map/save→template и native geometry не доказаны |
| R-03 | «Захват столицы» и «Битва за столицу» | Исследование завершено; подтверждённая часть реализована | Engine evidence: working IDs 1/3/4/5/6/8; самостоятельные triggers 2/7 не доказаны, не включены по одному label |
| R-04 | Турнир или финальная битва на 3–8 игроков | Исследование завершено; подтверждённая часть реализована | BattleCalendar/tests: подтверждены два отдельных противника; bracket на 3–8 игроков и большие alliance groups не подтверждены |
| R-05 | Строгая политика отсутствия порталов | Исследование завершено; подтверждённая часть реализована | Engine evidence + portal tests: authored endpoint controls сохраняются/экспортируются; native Direct→Portal fallback исключает гарантию zero portals |
| R-06 | Дополнительные AI профили и прочие крупные режимы | Исследование завершено; подтверждённая часть реализована | NativeAiCatalog/tests + UI: 15 exact noncampaign IDs; поведение none/test, naval/underground/new modes требует отдельного proof |

## Границы приёмки

Владелец проекта выбрал провести игровую проверку самостоятельно по готовому набору. Поля ручного журнала остаются pending до фактического запуска; автоматические результаты не подменяют эту проверку. Релиз не публикуется без согласования.

Исследовательские R-01…R-06 включают проверку возможности, а не обещание неподдерживаемой функции. Регистрация пользовательского native-профиля, самостоятельные triggers победы 2/7, многопользовательский турнир и полный запрет fallback-порталов не раскрываются как подтверждённые возможности. Поддержанные reader/editor/importer/AI-каталоги реализованы отдельно.

## Результаты локальной приёмки

Финальная сборка и тесты выполнялись через `scripts/Invoke-LowImpactChecks.ps1 -Mode BuildAndTest`: Release, .NET SDK 10.0.300, Idle, один logical CPU, MSBuild `-m:1`, без compiler server и параллельных тестовых коллекций. Результат: **0 ошибок, 0 предупреждений; 785/785 тестов, 0 skipped**. Первоначальная база имела 305 тестов.

`Invoke-PatchAcceptance.ps1 -Mode Ui` для `ru` и `en` прошёл 15 функциональных проверок в каждом языке и создал по 29 PNG. Проверены точное число игроков и ручные locks, сохранение полного фактического ввода, повторяемость Simple→Advanced, stale export, реальные reward variants, native conditional row, инспектор после сохранения и отказ от повторяющихся свойств в raw JSON. На каждой вкладке свойств/настроек проверяются bounds и пиксели текста кнопок применения/отмены. Снимки проверены визуально; главный предпросмотр целиком помещается в доступную область.

Установленный источник: Olden Era **0.81.04**, Steam build **25672315**. `Invoke-PatchAcceptance.ps1 -Mode Kit` проверил **168/168** случаев на seeds 1729/424242: 53 Current presets и 31 отдельный сценарий. Полный reference catalog, запрошенные поля, исходные настройки, повторяемость и второй JSON импорт проверяются независимо.

Корпус: **138** файлов; **137** прошли сохранение исходных значений/unknown fields, **1 безопасно отклонён**. В `Jebus Cross F x4.rmg.json` повторяются разные корневые `mandatoryContent`. Это не lossless success: receipt имеет `PreservesValues=false`, `SafelyRejected=true` и причину отказа. Hash исходника проверен, файл не менялся. Сравнение значений не ослаблено. Допустимые нормализации typed чисел/коллекций отдельно перечислены в отчёте.

Для владельца сформированы **20 пар `.rmg.json`/`.oetgs`**, RU/EN инструкции, SHA-256 manifest и `manual-results.json` со статусом pending. Набор требует нескольких игровых seeds, lobby/hotseat, второй недели, условий победы, сохранения/загрузки. Игра автоматически не запускалась, её папки и обычные preferences Aurora не использовались для записи.

Локальные результаты лежат в `artifacts/game-patch-08104/`; они не входят в Git и не являются опубликованным релизом. Сборка single-file win-x64 подготовлена через `Invoke-LowImpactChecks.ps1 -Mode Publish`, сохраняет текущий технический номер 1.11.0 и отличается именем тестового набора и Git revision. Опубликованный релиз 1.11.0 не заменён. Скрипты PowerShell проходят синтаксическую проверку, итоговый diff проходит `git diff --check` с настройкой CRLF.

Независимое ревью проводилось по генерации/валидатору, данным/экспорту и native/editor направлениям. Подтверждённые пропуски закрыты регрессиями; неподтверждённые игровые эффекты остались явно обозначенной ручной приёмкой.
