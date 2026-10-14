# AuroraRMG v1.12.0

[Скачать / Download](https://github.com/sany86russ/AuroraRMG/releases/tag/v1.12.0) · [Full changelog](https://github.com/sany86russ/AuroraRMG/compare/v1.11.0...v1.12.0)

## Обновление под Olden Era 0.81.04

AuroraRMG v1.12.0 обновляет генератор под **Heroes of Might and Magic: Olden Era 0.81.04**. В настройках появились настоящие альянсы, новые параметры наполнения, календарь боёв и правила свежего патча. Aurora создаёт собственный `.rmg.json`; игровой мир, размещение объектов и окончательную геометрию строит генератор самой игры.

- В окне **«Команды и параметры карты»** можно выбрать актуальный профиль 0.81.04 или прежний профиль Aurora. Новый профиль использует проверенные таблицы богатства, пулы содержимого и параметры игрового формата. Legacy сохраняет прежние таблицы и обычное преобразование seeds; исправления ошибок и проверки ссылок действуют в обоих профилях.
- Доступны **53 актуальных пресета: 43 адаптированных и 10 новых командных/кооперативных**. Прежние **43 Legacy-пресета** сохранены отдельной группой.
- Размер **256×256** входит в обычный список. Экспериментальные размеры доступны до **512×512** и требуют отдельной проверки генерации и производительности в игре.
- Каталоги читаются из установленной игры. При изменении игровых данных они обновляются; повреждённый или несовместимый кэш пересобирается. Отсутствие нужного источника или зависимостей показывается в проверке.

## Настоящие команды и кооперативные карты

- Состав команд теперь записывается в шаблон как реальные назначения игроков в альянсы. Поддерживаются готовые составы, ручные назначения, отдельные игроки FFA и неравные команды, например **2 на 1** или **1 против 7**.
- Случайное распределение перемешивает игроков с сохранением выбранных размеров команд и зависит от seed шаблона. Ручные назначения сохраняют свой состав; редактирование одного слота не сбрасывает остальные.
- Альянсы можно закрепить целиком или отдельно для ручных слотов. Число допустимых альянсов задаётся явно. Человеческие и компьютерные слоты, фракции и сложность ИИ выбираются в игровом лобби или hotseat.
- Добавлены **командные территории, коридоры и общая арена**: союзные проходы, общий тыл, фронт и нейтральные промежутки между противниками. Для зеркальных командных территорий синхронизируются роли зон, размер, наполнение, охрана и города; дополнительные порталы распределяются симметрично.
- Для меньшей команды можно отдельно увеличить ресурсный бюджет. Отчёт сравнивает условия отдельных игроков и целых команд. Одинаковые настройки ветвей не означают одинаковую геометрию или гарантированный баланс партии.

## Богатство, опасность, города и темп

- **Пять уровней богатства** управляют ресурсами и объектами по актуальным таблицам. Ручные проценты ресурсов и структур остаются отдельными настройками, поэтому богатую карту можно сделать с редкими объектами или наоборот.
- **Шесть уровней опасности** меняют охрану и её развитие в генерации Aurora. Сила нейтралов внутри зон и охрана границ/порталов настраиваются отдельно. Эти параметры не заменяют сложность ИИ в лобби.
- Наполнение и охрана учитывают роль зоны и удалённость от стартов по графу. Режимы темпа помогают выбрать ранний контакт, обычное развитие или более долгое исследование; это настройка структуры карты, а не обещание поведения ботов.
- Общее число нейтральных городов и максимум городов в одной зоне задаются независимо от богатства. Распределение учитывает командные ветви и общие территории; цель удержания не удаляется ради уменьшения числа городов.
- Можно выбрать асимметричную карту, равные стартовые условия или симметрию команд. Если выбранную структуру, глубину нейтральных промежутков или лимит порталов нельзя совместить, программа сообщает о конфликте настроек.
- Добавлены **бюджеты на 10 пригодных клеток** в игровом формате. Пригодную площадь считает игра. Aurora показывает коэффициенты отдельно и не выдаёт их за известную итоговую стоимость всей зоны.

## Условия победы, герои и календарь боёв

- Доступны **стандартные условия, удержание столицы, удержание города, решающая битва, турнир и «Падение стража»**. Проверяются нужные цели и допустимые сочетания правил. «Падение стража» использует условие потери важного стартового героя.
- Ключевой объект, стартовый город и цель удержания редактируются отдельно. Для удержания города задаются число дней и поведение таймера при смене владельца.
- **«Начальный лимит героев» — вместимость найма**, с учётом принадлежащих игроку стартовых городов и прироста на город. В актуальном профиле невозможный точный лимит отклоняется с объяснением. Этот параметр не является подтверждённым управлением числом созданных стартовых героев. В режиме одного героя карточка показывает фактические лимиты 1–1.
- Турнир принимает выбранные даты боёв в привычном счёте дней, начиная со **дня 2**. Срок предварительного объявления задаётся отдельно; программа проверяет порядок дат и формирует относительные интервалы игрового формата.
- Исправлена настройка **сохранения армии в турнире**: выбранное значение учитывается при экспорте. Поддерживаются последовательные бои, включая соседние дни.
- Для финальной битвы отдельно задаются выбранный день, регистрация и продолжительность. Прежние настройки запуска арены сохранены. **Турнир и подтверждённый экспорт финальной битвы рассчитаны на 1 на 1 — двух отдельных противников**; неподтверждённые составы отклоняются.

## Каталог, награды, реки и развитие

- Новый каталог показывает реальные русские и английские названия, игровые идентификаторы, категории и варианты наград из установленной игры. Можно искать объекты, заклинания и артефакты, смотреть связанные награды и принадлежность к содержимому RMG. Отметка «есть в RMG» не гарантирует размещение объекта на любой карте.
- Для свитков и других объектов с вариантами выбирается **существующий индекс из игровой конфигурации**. Произвольное поле «уровень объекта» не подменяет вариант награды. Известный неверный индекс блокируется в редакторе пулов, общей проверке и экспорте пакета.
- Пулы различают вес группы, вес объекта, вариант награды и зависимость от списка. **Вес — относительная вероятность**, а обязательность и максимальное количество задаются отдельно. Поддерживаются дробные веса; нулевой вес сохраняется, отсутствующий использует значение по умолчанию. Нечисловые значения, переполнение и положительные веса, исчезающие при преобразовании в игровой формат, отклоняются.
- Пользовательские пулы и нужные зависимости встраиваются в шаблон. Уже встроенные определения автора сохраняются; локальная библиотека не подменяет их своей версией.
- Добавлены реки и расстояние между притоками, начальные запасы для найма и настройки недельного прироста. Параметры доступны для зон и соответствующих городов/аванпостов; настройки первой недели и обычного роста разделены.
- Законы фракций, гильдия магии и дополнительное обучение магии отключаются отдельными переключателями. Реакции охраны в актуальном профиле используют проверенный порядок игрового перечисления.
- Доступны прочитанные из игры идентификаторы **15 профилей ИИ вне кампании**. Название профиля само по себе не доказывает пассивность, особое поведение или сложность бота.

## Редактор и работа со старыми шаблонами

- В **свойствах шаблона** редактируются общие параметры, альянсы, правила, карточка в игре и реки всех вариантов карты. Инспектор дополнен ключевыми объектами, ростом найма, реакциями охраны и параметрами соединений.
- **Массовая правка зон** применяется целиком и отменяется одним Undo. При переименовании, удалении и зеркальном редактировании обновляются известные ссылки, включая дороги и объекты на концах портальных соединений.
- Проверка проходит все варианты карты, назначения игроков, цели победы, ссылки, числовые диапазоны и зависимости содержимого. Неизвестные поля сохраняются при чтении и экспорте; отсутствующие необязательные настройки не добавляются без необходимости.
- Замечания к импортированному шаблону остаются видимыми. Несвязанная правка описания не стирает прежнюю ошибку; сохранение может потребовать подтверждения предупреждений. Новые неверные правила или ссылки блокируются перед сохранением.
- **Повторяющиеся свойства JSON отклоняются до импорта**, в том числе во вложенных и неизвестных блоках. Программа не выбирает последнее значение и не объединяет разные массивы. Исходный файл при отказе остаётся целым.
- Исправлены сценарии сохранения и дальнейшего редактирования инспектора, перенос полного ввода из Simple в Advanced и отображение главного предпросмотра. Если выбранный темп меняет структуру графа, превью и сводка показывают фактически созданную структуру; переключение языка сохраняет правильное описание. Исходные настройки для повторения остаются в снимке. Окна и новые параметры проверены в RU/EN.

## Карточка шаблона и отчёт баланса

- Карточка в игре описывает фактические альянсы, размер, включённые условия победы, лимиты героев, богатство, опасность и темп. Для турнира и финала указываются даты и настройки армии; ручные проценты наполнения и охраны тоже видны.
- Сохраняется описание автора, добавляются подходящие теги, seed шаблона, профиль и версия алгоритма. Карточка отражает экспортированные правила, а не только название выбранного пункта интерфейса.
- Отчёт баланса показывает стартовые города и ресурсы, командные показатели, доступ к нейтральным городам и охраняемому наполнению. Число переходов и сумма настроенных бюджетов охраны — отдельные минимумы; они могут относиться к разным путям и не являются стоимостью реального боя.
- При бюджетах на площадь итоговая экономика отмечается как неизвестная. Флаги порталов показывают параметры объектов на концах соединения; из них не выводится гарантия одностороннего перемещения.

## Повторение, обмен и пакетная генерация

- Сохраняется полный снимок **фактически использованных настроек**, включая параметры, которые обычные элементы интерфейса не показывают полностью. Повторение требует того же seed шаблона, настроек и версии алгоритма. Seed игрового мира записывается отдельно и задаётся в самой игре.
- **ZIP-пакет** содержит `.rmg.json`, памятки RU/EN и выбранные дополнительные материалы — например, настройки и PNG. В игровую папку `map_templates` переносится только `.rmg.json`. Точные настройки с распознанными секретами не включаются в общий пакет без исправления; их можно явно исключить.
- **Пакетная генерация** создаёт результаты для списка seeds или выбранных пресетов в отдельной папке, проверяет каждый шаблон и допускает отмену. Существующие файлы заменяются только при явно выбранном режиме; отчёт сообщает об успехах и ошибках каждого результата.
- Запись шаблонов и архивов защищает предыдущий файл при сбое. Идентификаторы профилей и контрольные суммы помогают проверить, какой результат был сохранён и передан.
- **Диагностический архив создаётся локально и никуда не отправляется.** В него входят только явно выбранные логи с ограничением размера и удалением известных секретов, путей и локальных идентификаторов. Сокращённые и исключённые логи перечисляются в результате. Слишком большие JSON, включая полную схему установленной игры 0.81.04, заменяются размером и SHA-256 в диагностической копии; исходные настройки сохраняются. Перед передачей архив нужно просмотреть.

## Нативные схемы и импорт игровых настроек

- Добавлен отдельный редактор **`.mrmg`-схем**: просмотр и изменение условных параметров, проверка ссылок и сохранение копии вне папки игры. Это инструмент работы с форматом; регистрация своей схемы в игровом списке выбора не подтверждена.
- Импорт нативного JSON или compact-кода переносит подтверждённые параметры в Advanced. Игровые даты со счётом от нуля переводятся в привычные дни. Compact-код не содержит лимитов героев, поэтому текущие лимиты сохраняются.
- Неизвестные и непереносимые входы сохраняются в снимке с предупреждениями. Импорт не воспроизводит геометрию или случайные решения встроенного генератора игры и не превращает готовую карту или сохранение в исходный шаблон.

### Выполненные локальные проверки и ограничения

- Финальная сборка **v1.12.0 прошла без ошибок и предупреждений, 785/785 тестов, 0 пропущенных**. Локальные проверки выполнялись последовательно, с низким приоритетом и ограничением на один логический процессор.
- Настоящие WPF-сценарии RU/EN прошли **по 16 функциональных проверок**: число игроков, назначения и закрепление альянсов, сохранение ввода, повторение Simple → Advanced, блокировка устаревшего результата, каталог наград, нативный редактор, редактирование после сохранения и правильная структура превью после переключения языка. Обновлены **28 прежних снимков**, добавлены **18 новых**; снимки проверены визуально. Все **53 файла ReadyMaps** прошли строгую проверку по каталогу установленной игры.
- Офлайн-набор для 0.81.04 прошёл **168/168 случаев** на двух фиксированных seeds: 53 актуальных пресета и 31 дополнительный сценарий. Проверены фактические поля, неизменность исходных настроек, повторяемость и повторное чтение JSON.
- Из **138 установленных шаблонов 137** прошли чтение и экспорт с сохранением исходных значений и неизвестных полей. **Один безопасно отклонён:** `Jebus Cross F x4.rmg.json` содержит два разных корневых `mandatoryContent`. Это отказ из-за неоднозначности, а не успешное сохранение без потерь; исходник не изменён.
- Подготовлены **20 пар `.rmg.json`/`.oetgs`** и инструкции для ручной приёмки. **Игровой тест не выполнялся.** Автоматические проверки не подтверждают играбельность, размещение любого объекта из каталога, фактическое число стартовых героев или поведение правил на нескольких игровых seeds.

Самостоятельные режимы «Захват столицы» и «Битва за столицу» (`win_condition_2`/`7`), турнир или финал на 3–8 участников, регистрация пользовательской нативной схемы и гарантированный полный запрет порталов не объявляются поддержанными функциями. Подтверждённые условия используют IDs **1, 3, 4, 5, 6 и 8**. Переключатель порталов управляет дополнительными соединениями Aurora; игра может создать портал вместо неудавшегося прямого прохода. Морские и подземные режимы не добавлены по одному наличию поля или класса в игровых файлах.

---

## Update for Olden Era 0.81.04

AuroraRMG v1.12.0 updates the generator for **Heroes of Might and Magic: Olden Era 0.81.04**. The settings now include actual alliances, new content controls, battle calendars and rules from the patch. Aurora creates its own `.rmg.json`; the game builds the world, places objects and determines the final geometry.

- **Teams and map options** offers the current 0.81.04 profile and the previous Aurora profile. The current profile uses verified richness tables, content pools and game-format parameters. Legacy retains the previous tables and normal seed conversion; bug fixes and reference checks apply to both profiles.
- There are **53 current presets: 43 adapted presets and 10 new team/cooperative presets**. The previous **43 Legacy presets** remain in a separate group.
- **256×256** is now a normal size. Experimental sizes extend to **512×512** and require separate in-game generation and performance checks.
- Catalogs are read from the installed game and refreshed when game data changes. Damaged or incompatible caches are rebuilt. Validation reports missing data sources and dependencies.

## Actual teams and cooperative maps

- Team membership is now exported as actual player-to-alliance assignments. Ready-made compositions, manual assignments, individual FFA players and unequal teams such as **2 vs 1** or **1 vs 7** are supported.
- Random assignment shuffles players while retaining the selected team sizes and uses the template seed. Manual assignments retain their membership; editing one slot does not reset the others.
- Alliances can be locked globally or individually for manual slots. The maximum alliance count is explicit. Human/AI slots, factions and AI difficulty are selected in the game lobby or hotseat.
- New **team regions, lanes and a shared arena** provide allied passages, shared rear areas, fronts and neutral buffers between opponents. Mirrored team branches share zone roles, size, content, guards and towns; additional portals are distributed symmetrically.
- A separate resource multiplier can help the smaller team. The report compares individual players and whole teams. Matching branch settings do not guarantee identical in-game geometry or a balanced match.

## Richness, danger, towns and pacing

- **Five richness levels** control resources and objects using current tables. Manual resource and structure percentages remain independent, so a rich map can have sparse objects or the reverse.
- **Six danger levels** adjust guards and their development in Aurora generation. Neutral strength inside zones and border/portal guards have separate controls. These settings do not replace lobby AI difficulty.
- Content and guards account for zone roles and graph distance from starts. Pacing options support early contact, normal development or longer exploration; they change the map structure and do not promise particular AI behavior.
- The total neutral-town count and maximum towns per zone are independent of richness. Distribution accounts for team branches and shared areas; reducing town counts does not silently remove a hold objective.
- Choose asymmetric maps, equal starting conditions or team symmetry. Conflicting layouts, buffer depths and portal limits produce an explanation.
- New **budgets per 10 eligible cells** use the game's format. The game determines eligible area. Aurora reports coefficients separately and does not present them as a known total zone value.

## Victory conditions, heroes and battle calendars

- Available conditions are **Classic, Capital Hold, City Hold, Final Battle, Tournament and Guardian's Demise**. Required targets and supported rule combinations are checked. Guardian's Demise uses the loss condition for the important starting hero.
- Key objects, starting towns and hold targets are separate properties. City Hold exposes the hold duration and timer-reset behavior when ownership changes.
- **Starting hero limit means hiring capacity**, accounting for owned starting towns and the per-town increment. In the current profile, an unattainable exact limit is rejected with an explanation. This is not verified control over the number of heroes spawned at the start. Single Hero cards show the effective 1–1 limits.
- Tournament battles use familiar human day numbers, starting on **day 2**. The announcement lead is separate; the generator checks chronological order and produces the relative intervals required by the game format.
- Fixed **Save army in tournament** so the selected value reaches the export. Sequential battles, including consecutive days, are supported.
- Final Battle separates the selected day, registration and duration. Previous arena-calendar settings remain available. **Tournament and the verified Final Battle export require 1 vs 1: two individual opponents**; unsupported player compositions are rejected.

## Catalogs, rewards, rivers and development

- The new catalog displays actual RU/EN names, game identifiers, categories and reward variants from the installed game. Search objects, spells and artifacts, inspect linked rewards and check whether content is referenced by RMG. RMG membership does not guarantee placement on every map.
- Scrolls and other objects with variants use an **existing index from the game configuration**. An invented object-level field does not substitute for a reward variant. Known invalid indices are rejected by the pool editor, common validation and package export.
- Pools distinguish group weight, object weight, reward variant and list dependencies. **Weight is relative probability**; mandatory placement and count limits are separate. Fractional weights are accepted, explicit zero is retained and omitted weights use the default. Invalid numbers, overflow and positive weights that disappear in the native numeric format are rejected.
- Custom pools and required dependencies are embedded in the template. Existing author definitions are retained instead of being replaced by local-library versions.
- New controls cover rivers and tributary spacing, initial recruitment amounts and weekly growth. Zone and relevant town/outpost settings are available; first-week and regular-growth options are separate.
- Faction laws, magic guilds and additional magic learning have independent disable switches. Guard reactions in the current profile use the verified game-enum order.
- The **15 noncampaign AI profile identifiers** read from the game are available. A profile name alone does not prove passive behavior, special tactics or AI difficulty.

## Editing and existing templates

- **Template properties** covers general settings, alliances, rules, the in-game card and rivers in every map variant. The inspector adds key objects, recruitment growth, guard reactions and connection settings.
- **Bulk zone editing** applies as one transaction and can be undone in one step. Rename, deletion and mirror operations update known references, including roads and objects at portal endpoints.
- Validation checks every map variant, player assignments, victory targets, references, numeric ranges and content dependencies. Unknown fields survive import/export; absent optional settings are not added unnecessarily.
- Findings on imported templates remain visible. An unrelated description edit does not erase an existing error; saving may require acknowledging warnings. Newly invalid rules or references are blocked before saving.
- **Duplicate JSON properties are rejected before import**, including nested and unknown fields. The app does not select the last value or merge conflicting arrays. A rejected source file remains unchanged.
- Fixed saving followed by further inspector editing, transfer of complete Simple inputs to Advanced, and the main preview layout. When pacing changes the graph structure, previews and summaries show the structure actually generated, including after a language switch. The original inputs remain in the reproduction snapshot. New windows and controls were checked in RU/EN.

## Template cards and balance reports

- In-game cards describe actual alliances, dimensions, enabled victory conditions, hero limits, richness, danger and pacing. Tournament/Final Battle dates and army settings are included, along with manual content and guard percentages.
- Author descriptions are retained. Appropriate tags, the template seed, profile and algorithm version are added. Cards reflect exported rules rather than only the name of a selected interface option.
- Balance reports show starting towns and resources, team metrics, access to neutral towns and guarded content. Minimum hops and minimum configured guard-budget sums are separate measurements: they may refer to different paths and are not the cost of a real battle.
- With area budgets, total economy is marked as unknown. Portal flags describe endpoint-object settings and do not establish guaranteed one-way movement.

## Reproduction, sharing and batch generation

- A complete snapshot records the **inputs actually used**, including settings not fully represented by ordinary controls. Reproduction requires the same template seed, settings and algorithm version. The game-world seed is recorded separately and selected in the game.
- **ZIP packages** contain `.rmg.json`, RU/EN instructions and chosen optional materials such as settings and PNG previews. Only `.rmg.json` goes into the game's `map_templates` directory. Exact settings containing recognized credentials cannot be shared in a package without correction; settings can be explicitly excluded.
- **Batch generation** creates results for seed lists or selected presets in a separate folder, validates each template and supports cancellation. Existing files are replaced only with an explicit overwrite mode; the report records each success or error.
- Template and archive writes protect the previous file on failure. Profile identifiers and checksums help identify the saved or shared result.
- **Diagnostic archives are created locally and are not sent anywhere.** Only explicitly selected logs are included, with size limits and removal of recognized credentials, paths and local identifiers. Truncated and excluded logs are listed. Oversized JSON, including the installed 0.81.04 game's full schema, is replaced by its size and SHA-256 in the diagnostic copy; original settings are retained. Review the archive before sharing.

## Native schemas and game-setting import

- A separate **`.mrmg` schema editor** supports conditional-parameter inspection/editing, reference validation and saving a copy outside the game directory. This is a format tool; registering a custom schema in the game's picker is not verified.
- Native JSON or compact-code import transfers verified parameters to Advanced. Zero-based native dates become human day numbers. Compact codes contain no hero limits, so existing limits are retained.
- Unknown and unmapped inputs remain in the snapshot with warnings. Import does not reproduce native geometry or RNG and does not recover a source template from a completed map or save.

### Completed local checks and limitations

- The final **v1.12.0 build completed with no errors or warnings and 785/785 tests, with 0 skipped**. Local checks ran sequentially at low priority on one logical CPU.
- Actual RU/EN WPF scenarios passed **16 functional checks per language**: player counts, assignments and locks, input snapshots, Simple → Advanced reproduction, stale-result blocking, reward catalogs, the native editor, editing after saving and the correct preview structure after language changes. **28 existing screenshots were refreshed and 18 new screenshots added**, with separate visual review. All **53 ReadyMaps files** passed strict validation against the installed game's catalog.
- The 0.81.04 offline suite passed **168/168 cases** on two fixed seeds: 53 current presets and 31 additional scenarios. Actual fields, unchanged source settings, reproduction and repeated JSON import were checked.
- Of **138 installed templates, 137** passed import/export with source values and unknown fields preserved. **One was safely rejected:** `Jebus Cross F x4.rmg.json` has conflicting root `mandatoryContent` properties. This is rejection of ambiguous input, not a successful lossless round-trip; the source was unchanged.
- **20 `.rmg.json`/`.oetgs` pairs** and manual-acceptance instructions were prepared. **No in-game test was performed.** Automated checks do not prove playability, placement of every catalog object, the actual number of starting heroes, or rule behavior across multiple game-world seeds.

Standalone Capital Capture and Capital Battle modes (`win_condition_2`/`7`), tournaments or finals for 3–8 participants, custom native-schema registration and a guaranteed complete portal ban are not advertised as supported features. Verified conditions use IDs **1, 3, 4, 5, 6 and 8**. The portal toggle controls Aurora's additional connections; the game may replace a failed direct passage with a portal. Naval and underground modes were not added based solely on the presence of a field or class in game files.
