# Zerg

Zerg is a native Windows app (C#, WPF, .NET 10) that shows your party's damage
and healing in FFXI, live, as you play.

## Requirement: the VibeXI addon

**Zerg needs the [VibeXI](https://github.com/HasayaFFXI/VibeXI) Ashita addon.**
The addon is Zerg's only data source: it reads the game's action packets and
writes one line per event to `%LOCALAPPDATA%\VibeXI\events\`, and Zerg reads
those files. Without the addon running in game, Zerg has nothing to show.

**Before using Zerg, install VibeXI by following the installation instructions
in the [VibeXI repository](https://github.com/HasayaFFXI/VibeXI).** Those
instructions are the authority on setting up the addon; this repository does
not repeat them.

## Getting Zerg

Download Zerg from the [latest release](https://github.com/HasayaFFXI/Zerg/releases/latest):
an installer, `Zerg-<version>-setup.exe`, or a zip with one `Zerg.exe` in
it (Windows 10 or 11, 64-bit; nothing else to install) and a `README.txt`
that says how to start. Both are made with `python tools/package.py` (the
installer needs Inno Setup 6: `winget install JRSoftware.InnoSetup`); a new
version merged to main is built and released by
`.github/workflows/release.yml`.

## Building from source

```
dotnet build Zerg.slnx
dotnet test Zerg.slnx
dotnet run --project src/Zerg
```

`CLAUDE.md` has the full development notes, `RULES.md` the event contract and
the reason behind every number Zerg prints, and `CHANGELOG.md` what each
version changed.
