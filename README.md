# ClosedEnv

Лаунчер **безопасного режима** для Windows: программа работает, а файлы, буфер обмена, камера, принтеры и остальные приложения этого компьютера остаются за рамкой.

MAX — первый готовый профиль. Тем же механизмом можно запустить произвольный `.exe` / установщик.

This is a Windows isolation launcher (hypervisor sandbox + optional WebView). It is **not** a MAX clone and does **not** reverse-engineer the messenger.

## Скачать

- [ClosedEnv-Web.exe](exe/ClosedEnv-Web.exe) — сразу `web.max.ru`, без лаунчера
- [ClosedEnv.exe](exe/ClosedEnv.exe) — окно профилей (веб, официальный клиент, generic)

Двойной клик по `ClosedEnv-Web.exe` ничего в BIOS не просит. Нужен только [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) (на Windows 11 обычно уже есть).

## Почему здесь MAX

MAX в репозитории не как «доказательство слежки» и не как вердикт приложению. Вокруг него просто было громче всего: слухи, заголовки, споры про данные. Автор эти истории не разделяет и ничего подобного не утверждает.

Профиль нужен как понятный пример. Мессенджер, которым многим приходится пользоваться, спокойно запускается в замкнутой среде — и при этом остаётся обычным MAX. Isolated mode про границы вашего компьютера, а не про войну с конкретным сервисом.

Как любой мессенджер, MAX ходит в свою сеть: иначе чаты не доходят. ClosedEnv не подменяет этот канал. Он закрывает **этот ПК** — документы, фото, буфер, устройства — от процесса внутри клетки.

Проект не связан с VK / MAX. Используйте на свой риск.

## Как запустить

| Файл / команда | Что открывается | Sandbox / BIOS |
| --- | --- | --- |
| `exe/ClosedEnv-Web.exe` | Сразу веб-MAX | Не нужен |
| `exe/ClosedEnv.exe` | Лаунчер (по умолчанию **MAX Web**) | Не нужен для веба |
| `ClosedEnv.exe --web` | То же, что ClosedEnv-Web | Не нужен |
| `ClosedEnv.exe --profile max-official` | Официальный клиент в Windows Sandbox | Нужны Pro и Sandbox |

Пересборка: `powershell -ExecutionPolicy Bypass -File scripts\publish.ps1` — готовые файлы появятся в `exe\`. Системный `dotnet` 3.1 для сборки не подходит.

## Режимы

| Профиль | Как запускается | Зачем |
| --- | --- | --- |
| **MAX Official** | Официальный клиент внутри Windows Sandbox | Полный десктоп, без установки MAX на хост |
| **MAX Web** | `web.max.ru` в изолированном WebView2 | Без MSI, фильтр чужих доменов |
| **Generic** | Любой `.exe` / `.msi` в той же песочнице | «Безопасный режим» для других Windows-программ |

Официальный клиент **не входит** в репозиторий. Песочница при необходимости скачивает установщик с `download.max.ru`.

## Требования

- Windows 10 22H2+ / Windows 11
- **MAX Web / ClosedEnv-Web.exe:** WebView2 Runtime, без Pro и без BIOS
- **Песочница (Official / Generic):** выпуск **Pro / Enterprise / Education**, виртуализация в BIOS, компонента Windows Sandbox
- **Пересборка:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Скрипт `publish.ps1` берёт SDK из `%LOCALAPPDATA%\dotnet-sdk`, если он там есть

Windows **Home**: гипервизорная клетка недоступна. Для MAX остаётся веб-режим.

## Сборка

```bat
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
```

Готово: `exe\ClosedEnv.exe` и `exe\ClosedEnv-Web.exe`.

Включение Windows Sandbox (только для Official / Generic; администратор, затем перезагрузка):

```bat
powershell -ExecutionPolicy Bypass -File scripts\enable-windows-sandbox.ps1
```

## Как это устроено

- **Sandbox:** лаунчер пишет `.wsb` и стартует `WindowsSandbox.exe`. На гостя монтируется только каталог профиля `%LOCALAPPDATA%\ClosedEnv\profiles\<id>\data`. Документы, рабочий стол и загрузки хоста не пробрасываются.
- По умолчанию выключены буфер обмена, принтер и камера. `ProtectedClient` включён. UNC-пути в mapped folders запрещены.
- **MAX Official:** гостевой файрвол оставляет публичные домены мессенджера (`max.ru`, `oneme.ru`, `download.max.ru` и соседние хосты из профиля). Это правила файрвола, не разбор TLS.
- **MAX Web:** WebView2 с отдельной папкой сессии и allowlist хостов. Камера и микрофон — только если вы их включили в лаунчере. Пустой блок «не робот» — обычно отрезанный iframe `id.vk.ru` (фильтр «Отрезанные» и хосты в шапке). `sdk-api.apptracer.ru` — телеметрия крашей VK Tracer, для виджета не нужна. ClosedEnv капчу за вас не решает. Тема интерфейса: кнопки «Светлая» / «Тёмная».
- **Журнал запросов:** снизу, сверху или справа. Полоса прокрутки всегда видна. Пишется также в `%LOCALAPPDATA%\ClosedEnv\profiles\max-web\requests.jsonl`. В песочнице — `guest.log` (время и домены, без расшифровки TLS).
- Вход в веб-MAX по QR — ограничение самого сервиса, не бага ClosedEnv.

Сессия Windows Sandbox одноразовая. Логин живёт в mapped `data\` (установка + junction типичных папок AppData).

## English

ClosedEnv is a Windows “safe mode” launcher. Download [ClosedEnv-Web.exe](exe/ClosedEnv-Web.exe) to open MAX Web immediately (no BIOS, no Windows Sandbox). MAX is an example profile because it was widely discussed — this project does not endorse rumors about the messenger. Isolation hides **host** files and devices; a messenger still talks to its own servers. An empty blue “not a robot” box usually means the allowlist blocked challenge scripts — not a missing captcha; ClosedEnv does not solve captchas. Apache 2.0.

## Лицензия

Apache License 2.0. См. [LICENSE](LICENSE).
