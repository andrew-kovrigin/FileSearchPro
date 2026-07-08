---
title: FileSearchPro - Network File Search Tool for Windows | C# WPF
description: Free tool to search files across network by IP. Search by name and content, hidden shares C$, match highlighting, up to 512 threads.
keywords: network file search, C# WPF, search by IP, file content search, hidden shares, Windows search, grep network, find files LAN, SMB search tool
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

A network file search tool for Windows. Supports hidden shares (C$, D$, ADMIN$), search by name and content, configurable exclusions.

## Screenshots

![FileSearchPro - Interface](FileSearchPro/assets/main.png)

## Features

- **Network scanning** — search by IP addresses and ranges (192.168.1.1-254)
- **Hidden shares** — C$, D$, Users, ADMIN$, IPC$
- **File name search** — glob patterns (*.pdf, report_*.*)
- **Content search** — multiple words via comma, match highlighting
- **Exclusions** — folders, files, extensions (configurable)
- **Preview** — text files with highlighted matches
- **Settings** — auto-save all parameters
- **Performance** — up to 512 threads, network caching (5 min)

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

1. Enter IP addresses or range in the "Network addresses" field
2. Select shares to search (C$, D$, Users)
3. Configure file name pattern or enable content search
4. Click "Start search"

### IP range examples

| Format | Description |
|--------|-------------|
| `192.168.1.1-254` | All addresses from .1 to .254 |
| `10.0.0.1,10.0.0.2` | Specific addresses |
| `172.16.0.1-50` | Subnet |

### Content search

1. Enable "Search text in files" checkbox
2. Enter words separated by commas: `password, secret, key`
3. Configure search extensions and exclusions
4. Matches are highlighted in yellow in the preview

## Project Structure

```
FileSearchPro/
├── [FileSearchPro.sln](FileSearchPro.sln)
├── [README.md](README.md)
├── [README_EN.md](README_EN.md)
├── [LICENSE](LICENSE)
├── [.gitignore](.gitignore)
└── [FileSearchPro/](FileSearchPro/)
    ├── [App.xaml](FileSearchPro/App.xaml)
    ├── [MainWindow.xaml](FileSearchPro/MainWindow.xaml) — UI Interface
    ├── [MainWindow.xaml.cs](FileSearchPro/MainWindow.xaml.cs) — UI Logic
    ├── [Models/](FileSearchPro/Models/)
    │   ├── [SearchConfig.cs](FileSearchPro/Models/SearchConfig.cs)
    │   ├── [ExclusionRule.cs](FileSearchPro/Models/ExclusionRule.cs)
    │   ├── [SearchResult.cs](FileSearchPro/Models/SearchResult.cs)
    │   └── [NetworkTarget.cs](FileSearchPro/Models/NetworkTarget.cs)
    ├── [Services/](FileSearchPro/Services/)
    │   ├── [NetworkScanner.cs](FileSearchPro/Services/NetworkScanner.cs) — Network Scanning
    │   ├── [FileSearchService.cs](FileSearchPro/Services/FileSearchService.cs) — File Search
    │   ├── [ExclusionService.cs](FileSearchPro/Services/ExclusionService.cs) — Exclusions
    │   ├── [AuthService.cs](FileSearchPro/Services/AuthService.cs) — Authentication
    │   └── [SettingsService.cs](FileSearchPro/Services/SettingsService.cs) — Settings
    └── [settings/](FileSearchPro/settings/)
        └── [exclusions.json](FileSearchPro/settings/exclusions.json)
```

## License

[MIT](LICENSE)

---

**Tags:** `network file search` `C# WPF application` `search files by IP` `Windows network scanner` `find files in network` `hidden shares search` `C$ D$ access` `file content search` `grep network` `search text in files`
