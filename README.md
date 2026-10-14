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

## Building from source

```
dotnet build Zerg.slnx
dotnet test Zerg.slnx
dotnet run --project src/Zerg
```

`CLAUDE.md` has the full development notes, `RULES.md` the event contract and
the reason behind every number Zerg prints, and `CHANGELOG.md` what each
version changed.
