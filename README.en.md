<p align="center">
  <img src="Assets/logo.png" width="128" alt="HiddenApp">
</p>

<h1 align="center">HiddenApp</h1>

<p align="center">Cut the internet of any Windows window with one click — and automate clicks with macros.</p>

<p align="center"><a href="README.md">Português</a> · <b>English</b></p>

---

## What it does

- **Lists the open windows** (the same ones as Alt+Tab), with each program's icon and a **live preview** of the selected window.
- **Block / allow internet** for a program with one click (or a double-click on the list). Connections that were already open are dropped too.
- **Select manually:** click the button and, within 3 seconds, click the window you want to block.
- **Favorites:** click the star next to a window to keep the program in your favorites, and block, allow or open it with one click (also from the tray icon).
- **Help balloons:** hover over a button to see what it does.
- **Blockable copy:** opens another instance of a program from a copy made of *hard links* (takes no space). Only the copy loses internet; the original keeps working — useful, for example, to have two windows of the same browser and block just one. A program can have **several copies**, each with its own name, profile and block.
- **System tray:** keeps running next to the clock; the icon menu lets you allow programs, switch theme and exit.
- **Start with Windows** (opens straight to the tray, without a UAC prompt).
- **Dark theme** (default) and **light theme**.
- **Languages:** Português (Brasil), English and 简体中文 (Simplified Chinese). By default it follows the Windows language; change it with the 🌐 button at the top.
- **Automatic updates:** tells you when there's a new version on GitHub and updates with one click (always asks first). Option to receive beta versions.

## Macro

On the **Macro** tab you create **macros** that click and press keys in a window by themselves:

1. Click the green **+** and choose the **target window**.
2. Add **points**: click **+ Add points** and then on the window preview — or place the mouse over the window itself and press **F9**.
3. For each point, choose the button, the action (click, double or hold), how many clicks in a row, the interval and the wait before the next point. Each point can have a **name or description**.
4. Press **F7** to start and **F8** to stop — in any program. The keys can be changed.

Steps can be **clicks** or **keys** (with Ctrl/Alt/Shift — use the green keyboard button). They run **in a loop**, in order.
You can **schedule** a macro ("Start at" 14:00, every day or only once), mark **favorites** (star, kept at the top) and
**reorder** the list. There are two modes:

| Mode | How it works | Note |
|---|---|---|
| **Real** (default) | Moves the mouse, clicks and presses keys for real | Works almost everywhere; the window must be in front (pauses by itself if it isn't) |
| **Background** | Sends clicks and keys straight to the window, without moving the mouse | You keep using the PC; not every program accepts it (many games ignore it) |

**Sharing macros:** the **export** and **import** buttons save and open `.hiddenmacro` files.

> ⚠️ Many **online games** forbid macros and auto clickers in their terms of service, and anti-cheat systems may
> **ban the account**. Check the rules before using it in games.

## Support the project ❤

HiddenApp is free. If it helps you, you can support it through
**[❤ GitHub Sponsors](https://github.com/sponsors/PauloHenriqueXD)** (credit card, once or monthly).
In Brazil you can also use Pix, via the **"❤ Support"** button inside the program.

## How it works

Blocking is done with **Windows Firewall** rules (inbound and outbound) for the chosen executable. The rules are stored in Windows, so the program stays blocked even with HiddenApp closed or after restarting the PC — until you allow it again.

Since Windows separates internet access by **process/executable** (not by window), blocking a window blocks the whole program. To block just one window of a program that has several, use the **blockable copy**.

## Requirements

- Windows 10 or 11 (64-bit)
- To **use**: just the `HiddenApp.exe` from the [Releases](https://github.com/PauloHenriqueXD/HiddenApp/releases) page (.NET is bundled)
- To **build**: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Run as **administrator** (the app asks automatically — it's required to create firewall rules)
- Windows Firewall enabled (if an antivirus replaces the Windows firewall, the rules have no effect)

## Build

```bash
dotnet build -c Release
dotnet publish -p:PublishProfile=Portatil   # single self-contained publish\HiddenApp.exe
```

The UI is built in code — don't use the Visual Studio Designer.

## Where data is stored

| What | Where |
|---|---|
| Blocked programs, settings, favorites, language | `%AppData%\HiddenApp\` |
| Firewall rules | Windows Firewall, rules starting with `HiddenApp - ` |
| Blockable copies | `X:\HiddenAppClones\` (on the same drive as the original program) |
| Auto click macros | `%AppData%\HiddenApp\macros.json` |
| Start with Windows | Task Scheduler, task `HiddenApp` |

## Limitations

- Programs that allow only one instance may hand the copy's window over to the original program; for known browsers HiddenApp already fills in a separate profile parameter.
- Microsoft Store apps, programs that reach the internet through a Windows service and games with anti-cheat may not work with the blockable copy.
- The copy's files are *hard links*: if the copy changes a file in its own folder, the original changes too.

## License, privacy and signing

- Open source under the **[MIT license](LICENSE)**.
- **[Privacy policy](PRIVACY.md):** HiddenApp doesn't collect or send personal data.
- **[Code signing policy](CODE_SIGNING_POLICY.md)** — Free code signing provided by
  [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).
- **Uninstall:** **Updates ▾ → Uninstall (remove everything)** removes the firewall rules, blockable copies and
  start with Windows (and, if you want, your settings). Then just delete `HiddenApp.exe`.

## Documentation (Portuguese)

- [ARQUITETURA.md](ARQUITETURA.md) — how HiddenApp works internally
- [CHANGELOG.md](CHANGELOG.md) — version history
- [CLAUDE.md](CLAUDE.md) — context and conventions for contributors
