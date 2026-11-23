# AuroraRMG v1.13.0

[Скачать / Download](https://github.com/sany86russ/AuroraRMG/releases/tag/v1.13.0) · [Full changelog](https://github.com/sany86russ/AuroraRMG/compare/v1.12.0...v1.13.0)

## Обновление под Olden Era 0.82.10

- Добавлен профиль **0.82.10** для установленной игры **Steam build 26640613**. Профили 0.81.04 и Aurora 1.11 доступны для прежних настроек. Обновлены все 53 ReadyMaps.
- В турнире и финальной битве можно выбрать чемпиона через **Шпиль Адского Света**. Генератор сохраняет расставленные вручную шпили и добавляет недостающие на изолированных сторонах турнира. Шпили также можно добавить в свойствах шаблона.
- Навыки и артефакты можно выбирать для запрета из каталога игры. В списке артефактов отмечены предметы, исключённые из случайной генерации 0.82.10. У каталога Pandora появились режим выдачи, вес и содержимое конкретного варианта награды.
- В параметрах охраны можно выбрать отдельный индекс награды объекта. Старые правила для всего объекта продолжают читаться. Проверка сообщает, если выбранного индекса в установленной игре нет.

## Редактор, сохранение и генерация

- В визуальном редакторе можно переключать **варианты карты**. Позиции зон сохраняются отдельно для каждого варианта. Массовая правка открывает активный вариант и пропускает пустые записи. Редактор схемы сохраняет выбранный узел при применении правок.
- Переименование прохода, вставка зоны и изменение числа городов обновляют ссылки в правилах размещения. Проверка учитывает установленный каталог игры и отдельные стороны турнира. У штатных шаблонов сохраняются исходные поля, включая лимиты по группам без SID.
- Простой и расширенный режимы сохраняют свои настройки отдельно. «Не сохранять» отбрасывает изменения текущего режима. Если файл `.oetgs` ошибочен, открытый документ остаётся доступен со своим именем и данными. Перед закрытием или автообновлением можно сохранить изменения открытых окон либо отменить действие.
- Для одинаковых настроек и seed одиночная и пакетная генерация Simple дают один результат. Лимит дополнительных порталов действует на весь турнирный шаблон.
- Готовый шаблон остаётся доступен при выключении PNG и при откате настроек к параметрам этого шаблона. Автообновление возвращает предыдущую версию при аварийном завершении сразу после запуска.

## Проверка игры и обратная связь

Автоматические проверки сверяют шаблоны с данными 0.82.10. Если во время партии возникла проблема, укажите версию игры, seed шаблона и отдельный seed игровой карты. Если возможно, приложите настройки `.oetgs` и сам шаблон `.rmg.json`. [Форма для сообщения об ошибке](https://github.com/sany86russ/AuroraRMG/issues/new?template=aurorarmg-bug.yml)

---

## Update for Olden Era 0.82.10

- Added the **0.82.10** profile for **Steam build 26640613**. The 0.81.04 and Aurora 1.11 profiles are available for existing settings. All 53 ReadyMaps have been refreshed.
- You can choose a champion at the **Hell Light Spire** for tournaments and final battles. The generator keeps manually placed spires and fills gaps on isolated tournament sides. You can also add spires from template properties.
- Pick skills and artifacts to ban from the installed game catalog. The artifact picker marks items excluded from random generation in 0.82.10. The Pandora catalog shows the reward mode, weight and contents for each configured variant.
- Guard overrides can target a specific reward index. Existing rules for the entire object still load. Validation identifies indices missing from the installed game.

## Editing, saving and generation

- The graph editor switches between **map variants** and saves zone positions for each one separately. Bulk editing starts on the active variant and skips empty entries. The schema editor keeps your selected node when applying an edit.
- Renaming connections, pasting zones and changing the town count update placement references. Validation uses the installed game catalog and accounts for isolated tournament sides. Imported stock fields are preserved, including list-based limits without an SID.
- Simple and Advanced keep their own settings. Discard drops the changes in the current mode. A damaged `.oetgs` leaves the open document and its path intact. You can save open work or cancel before closing the app or applying an update.
- With the same Simple settings and seed, single and batch generation produce the same template. The extra portal cap applies to the entire tournament template.
- Turning off PNG output or reverting to the settings that produced the current template keeps it ready for export. The updater restores the previous version if the new version crashes on startup.

## In-game feedback

Automated checks compare exports with 0.82.10 data. When reporting an issue from a match, include the game version, Aurora template seed and separate in-game map seed. Attach the `.oetgs` settings and `.rmg.json` template if available. [Bug report form](https://github.com/sany86russ/AuroraRMG/issues/new?template=aurorarmg-bug.yml)
