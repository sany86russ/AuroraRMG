<div align="center">

<img src="https://github.com/sany86russ/AuroraRMG/releases/latest/download/logo-256.png" width="120" alt="AuroraRMG"/>

# AuroraRMG

**Генератор случайных шаблонов карт для Heroes of Might and Magic: Olden Era**

[![Latest release](https://img.shields.io/github/v/release/sany86russ/AuroraRMG?style=flat-square&color=8B7CFF)](https://github.com/sany86russ/AuroraRMG/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/sany86russ/AuroraRMG/total?style=flat-square&color=22D3EE)](https://github.com/sany86russ/AuroraRMG/releases)

</div>

---

AuroraRMG — настольное приложение для Windows, которое генерирует файлы шаблонов
случайных карт `.rmg.json` для **Heroes of Might and Magic: Olden Era**. Вместо
ручного редактирования JSON вы настраиваете параметры карты в удобном интерфейсе
и нажимаете **«Создать шаблон»**.

## Установка

1. Скачайте **`AuroraRMG.exe`** из [последнего релиза](https://github.com/sany86russ/AuroraRMG/releases/latest).
2. Запустите — это автономный единый `.exe`, установка не требуется.

> Приложение само определяет путь установки Olden Era через реестр Steam и
> открывает диалог сохранения сразу в нужной папке `map_templates`.

## Автообновление

При запуске AuroraRMG проверяет последний релиз в этом репозитории. Если доступна
новая версия — в шапке появляется баннер **«Доступна новая версия · Обновить»**.
По кнопке приложение само скачивает новый `.exe` и перезапускается.

> Запуск с флагом `--minimized` пропускает проверку обновлений (удобно при старте
> вместе с игрой).

## Возможности

- **37+ готовых пресетов** (1v1 Classic / Single-Hero, FFA на 3–8 игроков, Hub,
  King of the Hill, Massacre и др.).
- Настройка размера карты, числа игроков, топологии зон, ландшафта, агрессии
  монстров, дипломатии, воды, условий победы и экономики.
- **Предпросмотр** компоновки зон перед сохранением.
- Тёмная тема **Aurora** (violet → cyan).

## Лицензия

[MIT](LICENSE)
