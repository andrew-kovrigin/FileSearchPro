---
title: FileSearchPro - Network File Search Tool for Windows | C# WPF
description: Free tool to search files across network by IP. Search by name and content, hidden shares C$, scan journal, localization RU/EN.
keywords: network file search, C# WPF, search by IP, file content search, hidden shares, Windows search, grep network, find files LAN, SMB search tool, scan journal
---

# FileSearchPro — Network File Search Tool for Windows

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![GitHub stars](https://img.shields.io/github/stars/andrew-kovrigin/FileSearchPro?style=social)](https://github.com/andrew-kovrigin/FileSearchPro/stargazers)

**English | [Русский](README.md)**

---

### Quick Navigation

| Section | Description |
|---------|-------------|
| [Features](#features) | What the tool can do |
| [Requirements](#requirements) | What you need to run |
| [Quick Start](#quick-start) | Build and run |
| [Usage](#usage) | How to use |
| [Structure](#project-structure) | Project code |
| [Download](https://github.com/andrew-kovrigin/FileSearchPro/releases) | Ready releases |
| [License](LICENSE) | MIT |

---

A network file search tool for Windows. Supports hidden shares (C$, D$, ADMIN$), search by name and content, real-time scan journal, localization RU/EN.

## Features

- **Network scanning** — search by IP addresses and ranges (192.168.1.1-254)
- **Hidden shares** — C$, D$, Users, ADMIN$, IPC$
- **File name search** — glob patterns (*.pdf, report_*.*)
- **Content search** — multiple words via comma, match highlighting
- **Exclusions** — folders, files, extensions (configurable)
- **Preview** — text files with highlighted matches
- **Scan journal** — real-time log of each host (IP, status, shares)
- **Journal export** — copy to clipboard + save to .log file
- **Settings** — modal window with timeouts, shares, filters
- **Localization** — switch RU/EN in settings
- **Monitoring** — thread count and memory in status bar

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Network access to target computers

## Quick Start

### Build

```bash
git clone https://github.com/andrew-kovrigin/FileSearchPro.git
cd FileSearchPro
dotnet build FileSearchPro.sln
```

### Run

```bash
dotnet run --project FileSearchPro
```

Or run `FileSearchPro.exe` from `bin\Debug\net8.0-windows\`

## Usage

1. Enter IP addresses or range in the "IP addresses" field
2. Click "Settings" to configure shares, timeouts, filters
3. Click "Start search"
4. Watch progress in the scan journal

### IP range examples

| Format | Description |
|--------|-------------|
| `192.168.1.1-254` | All addresses from .1 to .254 |
| `10.0.0.1,10.0.0.2` | Specific addresses |
| `172.16.0.1-50` | Subnet |

### Scan Journal

During search, the bottom panel shows a real-time journal:
- Each host with its status (online/offline)
- Discovered shares
- "Copy" and "Export .log" buttons for saving

### Timeouts

Configure in settings:
- **Ping** — host response wait time (default 300ms)
- **Shares** — share availability check time (default 3000ms)
- **Files** — file listing time (default 5000ms)

## Project Structure

```
FileSearchPro/
├── FileSearchPro.sln
├── README.md
├── README_EN.md
├── LICENSE
├── .gitignore
└── FileSearchPro/
    ├── App.xaml
    ├── MainWindow.xaml — UI Interface
    ├── MainWindow.xaml.cs — UI Logic
    ├── Models/
    │   ├── SearchConfig.cs
    │   ├── ExclusionRule.cs
    │   ├── SearchResult.cs
    │   ├── ScanLogEntry.cs
    │   └── NetworkTarget.cs
    ├── Services/
    │   ├── NetworkScanner.cs — Network Scanning
    │   ├── FileSearchService.cs — File Search
    │   ├── ExclusionService.cs — Exclusions
    │   ├── AuthService.cs — Authentication
    │   ├── SettingsService.cs — Settings
    │   └── LanguageManager.cs — Localization
    ├── Converters/
    │   ├── BoolToVisibilityConverter.cs
    │   └── ScanStatusToBrushConverter.cs
    ├── Resources/
    │   ├── Strings.ru.xaml
    │   └── Strings.en.xaml
    └── settings/
        └── exclusions.json
```

## License

[MIT](LICENSE)

---

**Tags:** `network file search` `C# WPF application` `search files by IP` `Windows network scanner` `find files in network` `hidden shares search` `C$ D$ access` `file content search` `grep network` `search text in files` `scan journal` `localization`
