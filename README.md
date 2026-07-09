---
title: FileSearchPro - Поиск файлов в локальной сети Windows | C# WPF
description: Бесплатный инструмент для поиска файлов по IP в локальной сети. Поиск по имени и содержимому, скрытые шары C$, журнал сканирования, локализация RU/EN.
keywords: поиск файлов в сети, network file search, C# WPF, поиск по IP, поиск по содержимому, скрытые шары, Windows поиск, file search tool, network scan, журнал сканирования
---

# FileSearchPro — Поиск файлов в локальной сети Windows

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![GitHub stars](https://img.shields.io/github/stars/andrew-kovrigin/FileSearchPro?style=social)](https://github.com/andrew-kovrigin/FileSearchPro/stargazers)

**[English](README_EN.md) | Русский**

---

### Быстрая навигация

| Раздел | Описание |
|--------|----------|
| [Возможности](#возможности) | Что умеет программа |
| [Требования](#требования) | Что нужно для запуска |
| [Быстрый старт](#быстрый-старт) | Сборка и запуск |
| [Использование](#использование) | Как пользоваться |
| [Структура](#структура-проекта) | Код проекта |
| [Скачать](https://github.com/andrew-kovrigin/FileSearchPro/releases) | Готовые релизы |
| [Лицензия](LICENSE) | MIT |

---

Инструмент для поиска файлов в локальной сети Windows. Поддержка скрытых шар (C$, D$, ADMIN$), поиск по имени и содержимому, журнал сканирования в реальном времени, локализация RU/EN.

## Возможности

- **Сканирование сети** — поиск по IP-адресам и диапазонам (192.168.1.1-254)
- **Скрытые шары** — C$, D$, Users, ADMIN$, IPC$
- **Поиск по имени файла** — glob-паттерны (*.pdf, report_*.*)
- **Поиск по содержимому** — несколько слов через запятую, подсветка совпадений
- **Исключения** — папки, файлы, расширения (настраивается)
- **Предпросмотр** — текстовых файлов с подсветкой найденных слов
- **Журнал сканирования** — реал-тайм лог каждого хоста (IP, статус, шары)
- **Экспорт журнала** — копирование в буфер обмена + сохранение в .log файл
- **Настройки** — модальное окно с таймаутами, шарами, фильтрами
- **Локализация** — переключение RU/EN в настройках
- **Мониторинг** — количество потоков и память в статус-баре

## Требования

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Сетевой доступ к целевым компьютерам

## Быстрый старт

### Сборка

```bash
git clone https://github.com/andrew-kovrigin/FileSearchPro.git
cd FileSearchPro
dotnet build FileSearchPro.sln
```

### Запуск

```bash
dotnet run --project FileSearchPro
```

Или запустите `FileSearchPro.exe` из папки `bin\Debug\net8.0-windows\`

## Использование

1. Введите IP-адреса или диапазон в поле «IP-адреса»
2. Нажмите «Настройки» для конфигурации шар, таймаутов, фильтров
3. Нажмите «Начать поиск»
4. Наблюдайте за прогрессом в журнале сканирования

### Примеры IP-диапазонов

| Формат | Описание |
|--------|----------|
| `192.168.1.1-254` | Все адреса от .1 до .254 |
| `10.0.0.1,10.0.0.2` | Конкретные адреса |
| `172.16.0.1-50` | Подсеть |

### Журнал сканирования

Во время поиска в нижней панели отображается журнал:
- Каждый хост с его статусом (online/offline)
- Найденные шары
- Кнопки «Копировать» и «Экспорт .log» для сохранения

### Таймауты

В настройках можно настроить:
- **Ping** — время ожидания ответа хоста (по умолчанию 300мс)
- **Шары** — время проверки доступности шары (по умолчанию 3000мс)
- **Файлы** — время перечисления файлов (по умолчанию 5000мс)

## Структура проекта

```
FileSearchPro/
├── FileSearchPro.sln
├── README.md
├── README_EN.md
├── LICENSE
├── .gitignore
└── FileSearchPro/
    ├── App.xaml
    ├── MainWindow.xaml — Интерфейс
    ├── MainWindow.xaml.cs — Логика UI
    ├── Models/
    │   ├── SearchConfig.cs
    │   ├── ExclusionRule.cs
    │   ├── SearchResult.cs
    │   ├── ScanLogEntry.cs
    │   └── NetworkTarget.cs
    ├── Services/
    │   ├── NetworkScanner.cs — Сканирование сети
    │   ├── FileSearchService.cs — Поиск файлов
    │   ├── ExclusionService.cs — Исключения
    │   ├── AuthService.cs — Авторизация
    │   ├── SettingsService.cs — Настройки
    │   └── LanguageManager.cs — Локализация
    ├── Converters/
    │   ├── BoolToVisibilityConverter.cs
    │   └── ScanStatusToBrushConverter.cs
    ├── Resources/
    │   ├── Strings.ru.xaml
    │   └── Strings.en.xaml
    └── settings/
        └── exclusions.json
```

## Лицензия

[MIT](LICENSE)

---

**Tags:** `network file search` `C# WPF application` `search files by IP` `Windows network scanner` `find files in network` `hidden shares search` `C$ D$ access` `file content search` `grep network` `search text in files` `scan journal` `localization`
