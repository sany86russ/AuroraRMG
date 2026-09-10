# AuroraRMG v1.11.0

[Скачать / Download](https://github.com/sany86russ/AuroraRMG/releases/tag/v1.11.0) · [Full changelog](https://github.com/sany86russ/AuroraRMG/compare/v1.10.0...v1.11.0)

## Фракции замков и замечания из обсуждения

- В редакторе основного и дополнительных городов доступны все аргументы фракции, включая `Match` и несколько ограничений `FromList`. Один аргумент вводится на строку; смена типа больше не стирает список.
- Выбор объекта-источника и кнопки «Такая же» / «Другая» создают правило совпадения или отличия фракции. Например, нейтральный замок можно привязать к старту игрока без ручного редактирования JSON. Владелец меняется независимо от фракции; очистка владельца больше не подменяет её правило.
- Переименование зоны обновляет ссылки `Match` и `differentFrom`. Проверка находит отсутствующие объекты-источники, ссылки на себя и циклы `Match`.
- В окне импорта и README RU/EN объяснено отличие рисунка из цветных областей от схемы HotA с линиями и подписями. Для структуры HotA рекомендован существующий импорт `.h3t`; распознавание линий по картинке не добавлялось.

## Редактирование без потери связей и настроек

- Удаление дополнительного города пересчитывает номера объектов в правилах фракций и дорогах, включая ссылки из других зон. Дороги к удалённому объекту убираются. `Match` на удалённый источник заменяется случайной фракцией; ограничения `differentFrom` на него удаляются. При удалении зоны применяется та же очистка, а статус сообщает о сбросе правил.
- При копировании зоны её внутренние ссылки фракций направляются на копию; повторный признак стартового города снимается. Ссылки на другие зоны сохраняются.
- Зеркальные копии используют соответствующие источники фракций на своей стороне. Удаление объектов, переименование зон и превращение города в нейтральный согласованно применяются к паре при включённой синхронизации свойств.
- Отмена и повтор удаления восстанавливают позиции зон и зеркальные пары. Положения хранятся только для доступной истории изменений и очищаются вместе с ней.
- «Сделать нейтральным городом» сохраняет настроенную фракцию, снимает принадлежность старту игрока и включает охрану, если её не было. Сокращена подпись кнопки RU/EN; подпись «Тип объекта» расположена рядом с полем. Типы объектов показываются на выбранном языке, исходные идентификаторы доступны в подсказках.

## 🛡 Охрана границ и порталов до 800%

По просьбе игрока, которому после обновления ИИ стало недостаточно охраны на 500%, добавлена возможность усилить её до **800%**.

- **Расширенный режим:** на вкладке «Настройка зон» ползунок «Охрана границ/порталов» теперь позволяет выбрать **25–800%**.
- **Простой режим:** в списке «Охрана границ» появился отдельный пункт **«800%»**. Он задаёт ровно 800% при любом уровне хаоса.
- Пять прежних уровней, включая «Неприступная», сохраняют свои значения и диапазоны.
- Усиливается охрана проходов между зонами и порталов; сила нейтралов внутри зон не меняется. Выбор сохраняется и переносится из простого режима в расширенный.

800% означает восьмикратный бюджет охраны относительно 100%. Насколько долго такая охрана сдерживает ИИ, ещё предстоит проверить в игре.

## Надёжное сохранение и работа редактора

- Ошибка записи больше не обнуляет прежний шаблон или настройки. Ошибки сохранения карты показываются в окне и не завершают приложение. При сохранении в папку игры нужно подтвердить замену существующего шаблона.
- Каждое окно редактора получает свою копию карты: правки не меняют незаметно результат генератора или другое окно.
- Перед открытием другого шаблона редактор просит подтвердить потерю несохранённых правок; по умолчанию выбрана отмена. Загрузка сбрасывает режим зеркального редактирования предыдущей карты.
- Закрытие менеджера связей отменяет его правки. «Применить» сохраняет их вместе; переименование связи обновляет дороги, включая обмен именами двух связей.
- Копия стартовой зоны становится нейтральным городом: повторный старт того же игрока и дороги исходной зоны больше не попадают в копию. Удаление зоны исправляет оставшиеся ссылки на её охрану.

## Более точная проверка карт

- Соседство Proximity больше не считается проходом. Это учитывают генератор, проверка связности, восстановление связей при зеркальном редактировании и оценка баланса. Разобщённые старты получают предупреждение и оценку 0; намеренная изоляция в турнире сохранена.
- Проверка находит цепочки биомов, ссылающихся друг на друга по кругу, неверные номера объектов дороги и ссылки на связи другой зоны. Проверка привязок дорог использует общий индекс связей.
- Исправление связности может изменить карту, если прежняя конфигурация соединялась только через Proximity. Уже связанный проходами граф не перестраивается.

## Автообновление

- Перед установкой проверяются размер загрузки, формат Windows EXE и SHA-256, если контрольная сумма указана в релизе.
- Незавершённые и повреждённые загрузки удаляются; параллельные загрузки не используют один временный файл. Отмена закрывает окно после завершения очистки.
- Устанавливается только файл AuroraRMG.exe. Замена сохраняет предыдущую версию в `.previous`; неудачная замена оставляет рабочий файл целым.

## Интерфейс и навигация RU/EN

- Отдельная проверка локализации закрыла пропуски в легенде, подсказках зон, школах магии, категориях и названиях встроенных предметов/заклинаний. Каталог героев больше не запрашивает русский язык при выбранном EN. Открытые формы, таблицы и сообщения обновляются при смене языка без потери выбора и введённых значений. Добавлены автоматические проверки ключей ресурсов, параметров сообщений и переводов пресетов/контента.
- Переведены динамические подписи менеджера связей, настройки горячих клавиш, просмотрщик пулов и состояния обновления. Поиск контента учитывает переведённые названия и игровые идентификаторы. Ошибки JSON показывают строку, позицию и путь к проблемному полю.
- Игровые идентификаторы, поля JSON и пользовательские имена сохраняются без перевода. Системные окна выбора файлов и стандартные кнопки сообщений используют язык Windows.
- Привязка к сетке больше не закрывает заголовок редактора. Панель команд переносится по ширине; легенда сворачивается, ширина инспектора регулируется. «Авто-раскладка» пересчитывает позиции вместо восстановления прежнего расположения.
- Основное окно доступно от 1000×620, редактор — от 900×600. Исправлены перекрытие вкладок кнопкой редактора, обрезанные маленькие кнопки, длинные подписи и доступ к настройкам в невысоких окнах. Вкладки наполнения сохраняют порядок при переключении.
- Поиск больше не сбрасывает выбор предметов, заклинаний и значений охраны. Десятичные поля принимают точку и запятую; неверные числа выделяются. Настройки ориентации применяются только после проверки всех полей.
- Параметры стартовых бонусов проверяются до применения: множитель армии должен быть положительным конечным числом, ресурсы и прибавка движения — целыми неотрицательными. Множитель с запятой записывается в игровой формат с точкой. Неверное значение охраны больше не подменяется молча на 5000; уже переопределённые объекты не предлагаются повторно после поиска.
- Изменение ориентации и границы сохраняет дополнительные неизвестные поля шаблона, в том числе когда обычные поля очищены.
- После генерации в расширенном режиме простой режим больше не сохраняет другой результат под старой сводкой. Открытие файла настроек сразу показывает расширенный режим.
- Переведены сообщения импортёров и названия бонусов; исправлены обновление текста при смене языка и вводящая в заблуждение подсказка сида. Сид воспроизводит шаблон только вместе с настройками и версией генератора.
- Уточнены подсказки и README RU/EN: привязка и авто-раскладка меняют только схему, оценка равенства стартов не гарантирует игровой баланс, а размещение обязательного контента зависит от доступного места. Файл `.oetgs` описан как настройки расширенного режима.

## Функции и расход ресурсов

- Свои пулы контента теперь доступны в инспекторе, встраиваются в `.rmg.json` с игровыми именами полей и доступны в просмотрщике после повторного открытия. «Добавить все» в конструкторе учитывает поиск.
- Локальная библиотека своих пулов загружается и без установленной игры. При выборе в шаблон добавляются только выбранные пользовательские пулы; уже встроенные определения не заменяются версиями из библиотеки. Создание отклоняет повторяющиеся имена, а встраивание сохраняет дополнительные поля и отделяет данные шаблона от библиотеки.
- Оценка «Равенство стартов» учитывает доступ к нейтральным замкам в самом балле. Исправлены проверка достижимости заданного расстояния между игроками для топологии «Коридоры» и игнорирование базового количества нейтральных зон.
- Перемещение зоны обновляет только её связи. Превью простого режима строится по запросу; кэш иконок ограничен, списки конструктора пулов виртуализированы.
- Закрытые окна отписываются от смены языка, чтобы обработчики локализации не удерживали их в памяти.
- Импорт рисунка декодирует уменьшенное изображение, анализирует его вне потока интерфейса и объединяет частые изменения настроек. Поиск соседства пропускает внутренние пиксели; превышение лимита игроков больше не создаёт одинаковые имена стартов.

### Выполненные локальные проверки

- В превью «Дорожек» крайние зоны целиком помещаются в рамку; радиус учитывает расстояния внутри и между коридорами. Добавлены девять проверок для 2, 4 и 8 игроков с разной длиной коридоров.
- Режим проверки зеркала завершает работу без диалога сохранения служебного примера; оба отчёта RU/EN прошли валидацию. Все 43 встроенных пресета выгружены во временную папку и прочитаны как JSON.
- Пересняты все 28 снимков интерфейса для README RU/EN. Исправлено обрезание окон в режиме съёмки, включено отложенное превью и задан одинаковый пример для двух языков.
- Сборка без ошибок; пройдены все 305 тестов. Есть предупреждение NU1900 о недоступности проверки уязвимостей NuGet.
- Проверены 17 окон приложения, вкладки RU/EN при обычном и компактном размере, переключение языка с сохранением выбора и введённых данных. В ресурсах по 899 ключей для RU и EN; проверены названия 230 встроенных предметов/заклинаний и каталоги 108 героев на обоих языках.
- Дополнительно проверены реальные WPF-компоненты фракций и импорта RU/EN: аргументы, кнопки совпадения/отличия, сохранение нейтрального владельца, переключение языка и запись/чтение `.rmg.json`. Проверка ссылок фракций по всем 133 штатным шаблонам игры не выдала замечаний.
- Завершающий сценарий WPF RU/EN проверил удаление объектов и пар зон, пересчёт ссылок, превращение в нейтральный город, переименование, отмену/повтор, восстановление зеркального редактирования, создание зеркальной половины и сохранение/чтение результата.
- Проверены сохранение и повторная загрузка шаблонов, замена обновления и откат на временных файлах. Прочитаны и скопированы для редактирования все 133 шаблона установленной игры; успешное чтение не означает отсутствие замечаний валидатора или проверку играбельности.
- В локальном замере 100 генераций конфигурации на 8 игроков с 40 нейтральными зонами занимают менее 0,2 с суммарно, выделяется около 498 КиБ на шаблон. Это не замер генерации игрового мира или постоянного потребления памяти приложением.

Игровая проверка и реальное автообновление установленной версии не выполнялись.

---

## Town factions and community feedback

- The editor exposes all faction arguments for primary and additional towns, including `Match` and multiple `FromList` restrictions. Enter one argument per line; changing the selector type no longer erases the list.
- A source-object picker and Same faction / Different faction buttons create matching or exclusion rules. A neutral town can follow a player's start without editing JSON manually. Ownership changes independently of faction; clearing the owner no longer replaces the faction rule.
- Zone renaming updates `Match` and `differentFrom` references. Validation detects missing source objects, self-references and `Match` cycles.
- The import window and both READMEs explain the difference between colored-region sketches and HotA diagrams with lines and labels. Existing `.h3t` import is recommended for HotA structure; image-based line recognition was not added.

## Editing without losing references or settings

- Deleting an additional town updates object indices in faction rules and roads, including references from other zones. Roads to the removed object are dropped. A `Match` pointing at a removed source becomes a random faction; `differentFrom` restrictions on that source are removed. Zone deletion performs the same cleanup, and status text reports rule resets.
- Pasting a zone redirects its internal faction references to the copy and clears duplicate start-town flags. References to other zones are preserved.
- Mirrored copies use the corresponding faction sources on their own side. Object deletion, zone renaming and making towns neutral stay consistent across the pair when property synchronization is enabled.
- Undoing and redoing deletion restores zone positions and mirror pairs. Positions are retained only for available edit history and cleared with it.
- Make town neutral preserves the faction rule, removes player-start identity and enables guards if absent. Its RU/EN caption is shorter; the Object type label now sits next to its field. Object types display in the selected language, with original identifiers available in tooltips.

## 🛡 Border and portal guards up to 800%

A player reported that 500% guards were no longer enough after an AI update. Guards can now be increased to **800%**.

- **Advanced mode:** the “Border/portal guards” slider on the “Zone Setup” tab now supports **25–800%**.
- **Simple mode:** the “Border guards” list has a separate **“800%”** option. It sets exactly 800% at any chaos level.
- All five existing levels, including Impassable, keep their values and ranges.
- This strengthens guards on passages between zones and at portals; neutral strength inside zones is unchanged. The selection is saved and carries over from Simple to Advanced mode.

800% means eight times the guard budget of 100%. How long these guards hold back the AI still needs to be tested in the game.

## Safer saving and editing

- A failed write no longer truncates the previous template or settings. Template save errors are shown without terminating the app. Saving to the game folder asks before replacing an existing template.
- Each editor window owns its map: edits no longer silently change the generator result or another window.
- Before opening another template, the editor asks before discarding unsaved edits, with cancellation selected by default. Loading resets the previous map's mirror-editing mode.
- Closing the connection manager cancels its edits. Apply commits them together; connection renames update road references, including swapping two names.
- A copied start becomes a neutral town, without a duplicate player spawn or road anchors from the original zone. Deleting a zone repairs remaining guard-zone references.

## More accurate map validation

- Proximity no longer counts as a passage in generation, connectivity validation, connection repair during mirror editing or balance analysis. Disconnected player starts receive a warning and a score of 0; intentional tournament islands are preserved.
- Validation detects circular biome chains, invalid road object indices and anchors belonging to another zone. Road validation uses a shared connection index.
- Connectivity repair may change a map if its previous configuration relied on Proximity alone. Graphs already connected by passages are not rebuilt.

## Auto-update

- Downloads are checked for size, Windows executable format and SHA-256 when provided by the release.
- Incomplete or corrupt downloads are removed; concurrent downloads have separate temporary files. Cancellation closes the window after cleanup.
- Only AuroraRMG.exe is selected for installation. Replacement keeps the previous version in `.previous`; a failed replacement preserves the working executable.

## RU/EN interface and navigation

- A dedicated localization audit filled gaps in the legend, zone tooltips, magic schools, categories and built-in item/spell names. The hero catalog no longer requests Russian when EN is selected. Open forms, tables and messages refresh on language changes without losing selections or entered values. Automated checks cover resource keys, message parameters and preset/content translations.
- Dynamic connection-manager labels, hotkey settings, the pool viewer and update states are localized. Content search matches translated names and game identifiers. JSON errors show the line, position and path to the affected field.
- Game identifiers, JSON fields and user-defined names remain unchanged. System file pickers and standard message-box buttons follow the Windows language.
- Grid snapping no longer covers the editor title. Commands wrap to the available width; the legend collapses and the inspector can be resized. Auto-layout recalculates positions instead of restoring the previous layout.
- The main window supports 1000×620 and the editor 900×600. Fixed the editor button covering tabs, clipped small buttons and long labels, and inaccessible settings in shorter windows. Zone-content tabs keep a stable order when selected.
- Search preserves item, spell and guard-value selections. Decimal fields accept a dot or comma and highlight invalid values. Orientation settings apply only after every field passes validation.
- Starting-bonus parameters are validated before applying: army multipliers must be positive finite numbers, while resource and movement amounts must be nonnegative integers. A multiplier entered with a comma is written with a dot for the game. Invalid guard values no longer silently fall back to 5000; objects with existing overrides remain excluded after searching.
- Editing orientation and borders preserves additional unknown template fields, including when the regular fields are cleared.
- Advanced generation no longer leaves Simple mode able to save a different result under an old summary. Opening a settings file shows Advanced mode immediately.
- Import messages and bonus names are localized; language switching refreshes dynamic text. The seed help now explains that reproducing a template also requires the same settings and generator version.
- Help text and both READMEs clarify that snapping and auto-layout affect only the diagram, starting fairness does not guarantee game balance, and mandatory content placement depends on available space. `.oetgs` is described as Advanced-mode settings.

## Features and resource use

- Custom content pools are selectable in the inspector, embedded in `.rmg.json` with the game's field names and available in the viewer after reopening. Add all in the pool creator respects the search filter.
- The local custom-pool library loads even without an installed game. Selecting pools embeds only the selected custom definitions; existing embedded definitions are not replaced by library versions. Creation rejects duplicate names, while embedding preserves additional fields and separates template data from the library.
- Starting fairness now includes neutral-castle access in the score itself. Fixed the feasibility check for player separation in Lanes topology and the ignored basic neutral-zone count.
- Dragging a zone updates only its connections. Simple-mode previews render on demand; icon caching is bounded and pool-creator lists use virtualization.
- Closed windows unsubscribe from language changes so localization handlers do not retain them in memory.
- Sketch import decodes a reduced image, analyses it off the UI thread and coalesces rapid settings changes. Adjacency detection skips interior pixels; exceeding the player limit no longer produces duplicate start names.

### Completed local checks

- Lanes previews keep outer nodes inside the frame and account for spacing within and between corridors. Nine regression cases cover 2, 4 and 8 players with different corridor lengths.
- Mirror verification exits without a save prompt for its temporary example; both RU/EN reports passed validation. All 43 bundled presets were exported to a temporary directory and parsed as JSON.
- Refreshed all 28 interface screenshots for the RU/EN READMEs. Screenshot mode now captures complete dialog content, requests lazy previews and uses the same example for both languages.
- Build completed without errors; all 305 tests passed. NU1900 warns that the NuGet vulnerability service was unavailable.
- Checked 17 app windows, RU/EN tabs at normal and compact sizes, and language switching with selections and entered data preserved. Resources contain 899 keys per language; names for 230 built-in items/spells and catalogs of 108 heroes were checked in both languages.
- Additional checks exercised the actual RU/EN WPF faction and import components: arguments, matching/exclusion buttons, neutral ownership, language switching and `.rmg.json` writing/reading. Faction-reference validation reported no issues across all 133 stock game templates.
- A final RU/EN WPF scenario checked deleting objects and zone pairs, reference reindexing, making towns neutral, renaming, undo/redo, restored mirror editing, creating the mirrored half and saving/reloading the result.
- Verified template saving and reloading, update replacement and rollback using temporary files. All 133 templates from the installed game were read and copied for editing; successful reading does not imply a clean validator report or verified playability.
- A local benchmark of 100 templates with 8 players and 40 neutral zones took less than 0.2 s in total, allocating about 498 KiB per template. This measures neither in-game world generation nor the app's resident memory use.

In-game validation and an actual update of an installed version were not performed.
