# zerg — working notes

Zerg shows a HorizonXI party's damage and healing, live, as a **native
Windows 11 app**: C# / .NET 10 / WPF, `Zerg.exe`. It reads the event files the
VibeXI addon writes from inside the game. A player runs it with no console
and no browser, and its cards can float over the game as see-through panels
that never take the keyboard.

- `RULES.md` — the event contract and every counting and session rule, with
  its reason. Read it before changing any number Zerg prints.
- `CHANGELOG.md` — what each version was.

## Where it sits

```
FFXI ─▶ VibeXI addon (Lua, in-game; its own repo) ─writes─▶ %LOCALAPPDATA%\VibeXI\events\<Char>-<YYYYMMDDHHMMSS>.jsonl
                                                            │ tails
                                                      Zerg (this repository)
```

The addon (https://github.com/HasayaFFXI/VibeXI) is the only data source; the
bridge is one-way. Zerg shares no code with it or its calculators.

## Working with this user

- **The user handles git.** Don't commit, branch or push unless asked.
- **Verify in the running app**, not just by building ("Verifying a change").
  FFXI is often running behind the windows; grabs may include the game.
- **Ask before visual or behavioural choices** with more than one reasonable
  answer. Mechanical fixes don't need asking.
- **The naming rule.** Nothing in this repository names the browser app Zerg
  replaced or the in-game parser its columns are modelled on. Check:
  `grep -rniE --exclude-dir=.git "damage.?meter|metric[s]" .` outside `bin`,
  `obj` and `dist` must find nothing.
- **The settings file is theirs.** A check that writes
  `%LOCALAPPDATA%\VibeXI\zerg\settings.json` copies it aside first and puts it
  back. The user may change it between your runs: read it before blaming code.
- **Their Zerg is `dist/Zerg/Zerg.exe`** (desktop shortcut). Only one
  `Zerg.exe` runs at a time: a second start brings the running one forward and
  exits 0 silently. Never stop a process you didn't start; ask them to close
  it. Don't publish over `dist/Zerg` unless asked.
- **The registry is theirs too.** The Settings page's Parse files row writes
  `HKCU\Software\Classes\.zerg` and `Zerg.Parse`, pointing at whichever exe
  pressed it. Look first (`reg query HKCU\Software\Classes\Zerg.Parse /s`) and
  put it back.

## Environment

- **.NET SDK 10.0.401, x64**, in `C:\Program Files\dotnet`. Never use the x86
  SDK under `Program Files (x86)`. Set `DOTNET_CLI_TELEMETRY_OPTOUT=1` in the
  command when running `dotnet`.
- **Python 3.14** for `tools/*.py`. Set `PYTHONIOENCODING=utf-8` when a script
  prints `names`/`text` output (icon-font characters).
- **Windows 11 Home. The screens change from day to day** (one 2560×1600 at
  150 %, or a 3440×1440 primary at 100 % beside it): list them before
  assuming. Grabs at different scales don't compare.
- **Shells: Windows PowerShell 5.1 and Git Bash.** Never edit source with PS
  `Get-Content`/`Set-Content` (mangles UTF-8); use the editor tools. `.ps1`
  files here stay ASCII. Running a `.ps1` is blocked: run inline or pass
  `-ExecutionPolicy Bypass` for that process; don't change the policy.
- **Write scripts with the editor tools.** A here-document in a Bash command
  loses backslashes, and a Bash command over ~8,000 characters is cut short.
  A `\u` escape written through the editor arrives as the raw character
  (write `(char)0x2028`).
- **Inno Setup 6** (6.7.3, for the user alone, `%LOCALAPPDATA%\Programs\Inno
  Setup 6\ISCC.exe`) makes the installer; `tools/package.py` finds it. Its
  help decompiles with `hh.exe -decompile <dir> ISetup.chm`; script classes
  are in `topic_scriptclasses.htm`.
- **OneDrive holds the repo.** A folder delete can fail mid-sync: retry.
- PowerShell screen grabs and window moves are DPI-unaware; use `tools/drive.cs`.

## Commands

```bash
# all tests (under a second once built). The solution builds the app too;
# beside a running Zerg, name the test project instead
dotnet test Zerg.slnx
dotnet test tests/Zerg.Core.Tests

# run it: follows %LOCALAPPDATA%\VibeXI\events, or the folder set on the Settings page
dotnet run --project src/Zerg
dotnet run --project src/Zerg -- --events-dir <dir>

# the build the user's shortcut runs (close their Zerg first; empty dist/Zerg first)
dotnet publish src/Zerg -c Release -r win-x64 --self-contained false -o dist/Zerg

# the players' downloads, in dist/package: Zerg-<version>-win-x64.zip (self-contained
# single exe + packaging/README.txt) and Zerg-<version>-setup.exe (the same, installed
# by packaging/Zerg.iss); unsigned; prints the SHA-256s
python tools/package.py

# play a fixture into the file Zerg is following (press Start first)
python tools/replay.py <fixture.jsonl> <dir>\<live>.jsonl [--squeeze 60] [--extras]

# the synthetic fixture (tools/events/, ignored by git), or a parse ready to open
python tools/gen-test-events.py
python tools/gen-test-events.py --out <dir>\Hasaya_2026.07.30.jsonl
python tools/gen-test-events.py --export <file>.zerg --seed 7 --date 2026-07-31
```

`Zerg.exe` takes `--events-dir <dir>` (that run only; the setting is
unchanged) and `.zerg` paths, each opened in a View tab of its own. A second
launch with paths hands them to the running Zerg over the single-instance pipe
(`Native/SingleInstance`) and exits. Several files opened together from
Explorer are several launches at once: the first to claim the mutex listens
straight away; opens are serialised (`MainViewModel.opening`) and a file asked
for twice is read once.

**Build notes.** Warnings are errors (`Directory.Build.props`). Release builds
name their sources from `/_/` (`DeterministicSourcePaths`), so a stack trace in
the log of a published Zerg reads `/_/src/...`. After scripted
builds run `dotnet build-server shutdown` (a lingering node has held locks). A
running `Zerg.exe` locks its build output: beside one, build the test project
alone, or the app into another folder (`dotnet build src/Zerg/Zerg.csproj -o
<folder>`, which still cannot start while the other runs).

## Versions

The version lives in one place: `<Version>` in `Directory.Build.props`, applied
to every assembly. `AppInfo.Version` reads it back (log's first line, the
status line's `v1.0.0`). Never keep a copy anywhere else.

`major.minor.patch`; while major is 0: **minor** for anything a player would
notice (a feature, layout, counting rule, settings key, `.zerg` format),
**patch** for a fix to what that version already claimed. Refactors, tests,
tools and notes move nothing. The owner sometimes overrides this; 1.0.0 is
theirs to call.

Independent numbers, not this one: `addon.version` in the addon (the event
file's `meta` `v`), `ParseFile.Version` (`.zerg` format, raised only when an
older Zerg couldn't read the file), `assemblyIdentity version` in
`app.manifest` (leave at 1.0.0.0).

**Cutting a version** (git steps are the user's): change `<Version>`; in
`CHANGELOG.md` turn "Unreleased" into `## <version> — <date>` with a new empty
"Unreleased" above (changes are logged there when made, not at the cut);
`dotnet test Zerg.slnx`; commit; publish over `dist/Zerg` only if asked.
**Merging it to main releases it**: `.github/workflows/release.yml` runs when
`Directory.Build.props` changes on main and, if no release has that version,
tests, runs `tools/package.py` on a Windows runner, and publishes the GitHub
Release `zerg-v<version>` (which makes the tag; never tag by hand) with the
setup exe and the zip, the changelog's section as its notes. A version with no
`CHANGELOG.md` section fails it. It can also be run from the Actions tab.

## Layout

```
Directory.Build.props   the version, for every project
LICENSE                 the license (GPL-3.0); the downloads carry it as LICENSE.txt
.github/workflows/release.yml   a new version on main → a GitHub Release
src/Zerg.Core/          net10.0, no UI: everything testable
  EventFiles, EventTail, EventRecord   find and follow the newest *.jsonl; typed, strict reading
  EventReader, Roster, Events          records → damage rows, heals, roster
  Session                              the session clock (arm, latch, pause, snapped end)
  Counting, Totals, Healing            filter, credit, aggregate; the Party line
  Targets, DamageTypes                 isolating targets and damage types
  Compare, CompareSheet                A/B runs, and everything Compare prints
  Tracker                              the live session (or an opened parse: Tracker.Of);
                                       Count() is the whole pipeline in one call
  ParseFile                            .zerg export / import
  Replay                               an event file read whole: its bars, a range's session
  Format                               numbers and clocks: one rounding rule, US grouping
  LenientJson                          settings.json read so one bad value costs only itself
  Charts/                              where a chart puts things, no drawing
  Layout/                              SplitTree (panes' arrangement, pure functions),
                                       PaneDrops, Columns (table columns), PanelFit
  RowShade, RunColours, Hsv, Cast, Shades, LowMark, RowMarks   colour and mark rules
  SessionView, PanelOpacities, DrawRate, KeyChord
src/Zerg/               net10.0-windows WPF exe
  App.xaml(.cs)         fonts, measures, type styles; startup, single instance, crash dialog
  MainWindow            title bar, command bar, bands, the page, status line; tray, hot key
  MainViewModel(.Damage/.Healing/.Layout/.Parse/.Settings/.Pairs/.Replay)   one partial per concern
                        (.Pairs: a drill-down's second action, B, set beside it;
                        .Replay: an event file read into a View tab, cropped)
  CompareViewModel      the Compare section
  PickFilter            target / type filters and their tables
  PairPick              what one side of a pair is set to (Views/PairPicker draws it)
  ReplayRange, ReplayMenu   a replay tab's range (Views/RangeBand, Charts/RangeStrip);
                        the command bar's Replay menu
  Rows, DrillView, ViewedParse, Panels, PanelWindow, TrayMenu, EventFeed, Settings,
  AppTheme, Inks, Debouncer, Log
  Views/                one UserControl per card, plus the panels Zerg lays out with
                        (Pane, SplitPanel, PageStack, FigureBand, ChipLine, TabStrip, ...)
  Charts/               chart elements that only paint
  Themes/               Dark.xaml, Light.xaml (tokens), Panel.xaml, Controls.xaml (every control's look)
  Native/               Win32: TaskDialog, WindowPlacement, Overlay, SingleInstance,
                        FileAssociation, HotKey, TrayIcon, Metronome, WindowFrame, InFront
tests/Zerg.Core.Tests/  xUnit, Zerg.Core only (SavedParseTests pins a real 55-minute parse)
tools/drive.cs          drive the windows (dotnet run --file tools/drive.cs -- ...)
tools/replay.py, gen-test-events.py, package.py, make-icon.ps1,
  make-wizard-images.ps1 (the installer's pictures, from assets/zerg-1024.png)
packaging/README.txt    what a player reads first
packaging/Zerg.iss      the installer (per user; writes FileAssociation's keys, keep them alike)
packaging/wizard/       its pictures (made by tools/make-wizard-images.ps1; checked in)
```

## How it works

- **The loop.** `EventFeed` polls every 250 ms (a `FileSystemWatcher` only makes
  a poll early). New lines become `EventRecord`s on a pool thread, then rows in
  the `Tracker`. `MainViewModel.Recount()` calls `Tracker.Count(...)`, which
  returns a `Snapshot` everything is drawn from. Counts are coalesced to once
  per 250 ms and incremental (`Tracker.Scoped`), rebuilt only when something
  changes what already-read rows mean (the zero, a pause, the skillchain
  switch, `Roster.Version`). An idle session keeps no rows. A line breaking the
  contract is skipped, logged and counted on the status line.
- **The draw beat.** `Tick()` runs at the draw frequency (`DrawRate`, 30/s
  unless set, 1–60, kept by `Native/Metronome`) and rewrites only what the clock
  moves: the clock, DPS/HPS, a live cumulative chart's edge, and only where it
  is on screen (`MainViewModel.Seen`). Everything an event moves is redrawn by a
  count. **Charts never redraw per frame.**
- **Nothing is rebuilt that can be updated.** Rows are kept across counts and
  updated in place (`Rows.Sync`, keyed by real name even when names are
  hidden). `SplitPanel` never takes a child out: a new arrangement only changes
  rectangles. A settings flip (shading, accuracy mark) is a trigger and a
  repaint, never a count or a new row. A drill-down's hit list takes new hits
  at the top.
- **Sections.** `MainViewModel.Section`: Damage, Healing, View, Compare,
  Settings (never saved). `IsDamage`/`IsHealing` mean those cards are on screen
  (including View showing that side). View has a tab per opened parse
  (`ViewedParse`, its own read-only `Tracker`); `live` is always fed, `Shown` is
  what's drawn. Each parse and the session keep their own drill-downs and
  filters (`looking`). A replay is a View tab too (`ViewedParse.Range`):
  `Tracker.Replaying`, filled once the file is read off the UI thread
  (`Load`), its session given again on each move of the range (`Crop`,
  from `Replay.Crop`); a `.jsonl` opened or dropped there is replayed.
- **Panes.** Each section's cards are `Views/Pane`s in a `Views/SplitPanel`,
  placed by a tree of splits (`Zerg.Core/Layout/SplitTree`, installed trees in
  `PaneLayouts`). Edits (drag a rule, fold, carry a pane by its heading) are
  pure functions making a new tree; the panel raises `RearrangedEvent` →
  `MainViewModel.Rearrange`; saved per section in `settings.layouts`. A saved
  tree is repaired against the build's panes (`SplitTree.Repair`). Least pane
  320×132. At ≥700 units wide the page fills the window and doesn't scroll;
  narrower, panes stand in one column and the page scrolls (`PageView`,
  `PageStack`).
- **Tables: the row is the bar**, shaded in the character's colour to their
  share of the leader at the strength `RowShade` allows. Shading settings are
  set once on a card (`Views/Shading`) and inherited. Columns come from
  `Layout/Columns`; narrow tables drop columns (`Views/Shed`), then scroll.
  A heading that sorts is a `Views/SortHead` round the heading's `TextBlock`
  (the shedding style goes on it); each table's order is a `ColumnSort`
  (saved in `settings.sorts`), rows listed through `Zerg.Core/TableOrder`'s
  `Ordering`; a press redraws from the last count, never counts.
- **Filters.** Target and Type (`PickFilter`) cut together; each list shows
  what the other leaves. While a target is isolated every DPS is a dash
  (`MainViewModel.Rate`, which the beat goes through too). Never saved.
  `RULES.md` has the rules.
- **Compare.** `CompareViewModel` over a `CompareSheet`; redraws only on a
  change and only while on screen. A pair the player made (A's action
  against another of B's) is a `CrossPair`, a row before its row's actions.
- **Pairs in the drill-down.** A drill-down is a `DrillOn`: an action, and
  while two are side by side the other (B). `CompareSheet.Sides` gives the
  figures, `Compare.Shared` the bins, the same as Compare's. Run colours are settings; `AppTheme` makes
  six frozen brushes from them, swapped in one step.
- **Charts.** `Zerg.Core/Charts` decides where everything goes; `src/Zerg/Charts`
  paints. Give a chart a new immutable model per draw; rebuild models on
  `AppTheme.Changed`. The cumulative chart is five layers; the value axis and
  line ends stand between counts, so anything they depend on must join the
  list in `LineChart.Paint`. To UI Automation a chart is one image with a
  one-line summary.
- **Tokens and controls.** Every colour is a token in `Themes/Dark.xaml` /
  `Light.xaml`, asked for with `DynamicResource` (never `StaticResource`); no
  colour is written outside `Themes/`. `ThemeMode="None"`: every stock control's
  look is in `Themes/Controls.xaml`; states are style triggers; every style
  names its focus ring (`ZFocus`, `ZFocusInset`).
- **Colours and names.** `MainViewModel.ColorOf` and `NameOf` are the only
  places a character's colour and name are decided; panels use `PanelColorOf`.
- **Panels.** A floating panel is a second instance of a card with the same
  `DataContext`; `Views/Float.On` (inherited) is the only "floating" signal and
  every difference is a trigger on it. The window is borderless, transparent,
  topmost, a no-activate tool window, moved by hand (`Overlay.Drag`), and must
  never take the keyboard. Its text halo is one `DropShadowEffect`, **never on
  anything that moves with the clock**; bars in a panel don't ease.
- **Title bar.** Zerg's own, via `WindowChrome` (`TakeTitleBar`). Anything
  pressable in it needs `WindowChrome.IsHitTestVisibleInChrome="True"`. A
  maximized window's overhang is measured on every move (`Fit`).
- **Keyboard.** Tab goes through panes in the order they stand in:
  `SplitPanel` sets each pane's `Panel.ZIndex` to its reading order. The title
  bar and command bar are `Local` tab groups ordered by `TabIndex`. Lists whose
  rows can't be pressed don't take the keyboard, on purpose.

## State on disk

Under `%LOCALAPPDATA%\VibeXI\zerg\`:

- `logs\zerg.log` — startup, settings changes, file switches, session changes
  with exact times, crashes. **The first thing to read when something is wrong.**
- `settings.json` — read leniently: a value of the wrong shape reverts to its
  default alone (logged, original copied to `settings.json.bad`); nothing in it
  can stop Zerg starting. Keys are in `src/Zerg/Settings.cs`. Several are saved
  400 ms after a control stops moving (`Debouncer`); closing flushes them.

## Verifying a change

1. **Tests:** `dotnet test Zerg.slnx`. They cover `Zerg.Core` only. A change to
   a counting rule needs its own test and its reason in `RULES.md`.
2. **The running app.** First check for a running Zerg:
   ```powershell
   Get-CimInstance Win32_Process -Filter "Name like 'Zerg%'" | Select ProcessId, CommandLine
   ```
   Zerg counts only what arrives after Start, so play a fixture in:
   ```bash
   : > <dir>/Hasaya_2026.10.03.jsonl
   src/Zerg/bin/Debug/net10.0-windows/Zerg.exe --events-dir <dir> >/dev/null 2>&1 &
   dotnet run --file tools/drive.cs -- click <pid> Start
   python tools/replay.py tools/events/Hasaya_2026.07.30.jsonl <dir>/Hasaya_2026.10.03.jsonl --extras
   # replay.py prints first row time, last row time (Unix s), row count. Wait until 3 s
   # past the second (~27 s). Pausing earlier cuts the session short silently; a whole
   # one with --extras reads 153,687 damage in 00:24. Then one line that counts nothing,
   # so a count happens after the last row:
   printf '{"kind":"meta","t":%s}\n' "$(date +%s)" >> <dir>/Hasaya_2026.10.03.jsonl
   dotnet run --file tools/drive.cs -- click <pid> Pause
   ```
   `tools/drive.cs` commands: `front`, `fg`, `snap <png>`, `text` (all text,
   hidden included), `names` (controls shown), `click <name>`, `set <slider>
   <value>`, `type`, `keys`, `enter`, `scroll`, `where`, `hover`, `press`,
   `drag`, `at`, `chord`, `windows`, `tray`, `zoom`. Targets are `<pid>` or
   `h<hwnd>`. Things worth knowing:
   - **Floating panels, menus and tooltips are separate windows.** `windows
     <pid>` lists them (`tool noactivate topmost layered` for a panel); a menu
     and a tooltip both have no title — the menu is the one whose `names` has a
     `MenuItem`.
   - Useful names: a pane is `Group '<title>'`; its heading has `Move <pane>`,
     `Fold <pane>`, `Pop out <pane>`; rules are `Divider between <a> and <b>`
     (`set` moves one); tabs are `"Damage section"`, `"Healing section"`,
     `"Compare section"`, `"View <file name>"`; the gear is `Settings`; a
     character's actions open with `Actions of <character>`.
   - **UI Automation toggles a toggle without clicking it**: bind `IsChecked`
     and act on the change, never a `Click` handler or `Command`.
   - **`front` restores a maximized or snapped window** (`SW_RESTORE`) and does
     not bring Zerg back from the tray properly (use `tray h<hwnd> pick`).
   - **`chord` cannot send Tab**, sends arrows as the number pad's, and takes a
     single key by its character code (`'` is Right). Esc is `chord $'\x1b'`.
   - **A Save dialog ignores `type`**: use `keys h<hwnd> <full path>`, then
     `enter`. `enter` presses the real key at whatever is in front: `fg` first.
   - **The game can hold the pointer and the foreground.** Read the pointer back
     before a real press; keep on-screen runs short. Starting Zerg takes the
     foreground, from the game too.
   - Panels are topmost and cover each other: move them apart before pressing.
   - A UI Automation client makes every layout dearer from then on: don't
     measure CPU after `drive.cs` has read the window.
3. **`logs\zerg.log`** for what the app thought happened.

## Traps already hit

**WPF**
- **Window placement:** `SetWindowPlacement` twice in `SourceInitialized`,
  `WindowState = Maximized` after it. A record off every screen falls back to a
  default place.
- **A missing `DynamicResource` key is silent**; a missing `StaticResource`
  fails when the window is made, not at build. Remove a style after its last
  use; after renaming keys, list keys used against keys defined.
- **A control type with no style in `Themes/Controls.xaml` is drawn in Windows'
  look.** Styles are found by exact type: a subclass must name its style, and a
  class of Zerg's own has none unless it names one. Nothing covers `CheckBox`,
  `ComboBox`, `ListBox`, `TabControl`, `ProgressBar`, `GridSplitter`.
- **A token `Themes/Panel.xaml` lacks is the application's** (light under the
  light theme). Check a new control's keys against that file.
- **An element cannot ask for more than it was offered**: `SplitPanel` sets
  `PageStack.Needs` instead.
- **A user control isn't re-measured because something inside it was**, if its
  root keeps its size.
- **An element that paints itself** (`StateTag`, `Spark`) must not be
  `AffectsRender` on anything the beat sets.
- **Hidden, not Collapsed**, for an element a panel needs to measure.
- **An attached property isn't told a value equal to its default** (hence
  `Grow.Share` defaults to NaN).
- **A context menu raises `Closed` late or never**: watch `IsOpen`. A menu's
  separators look up `MenuItem.SeparatorStyleKey`. A menu wraps anything not a
  `MenuItem`/`Separator` in a `MenuItem`. A menu opened on the menu key's down
  shuts itself: open on key up.
- **A tooltip inherits font, weight and alignment** from its element;
  `ZToolTip` resets them.
- **An inherited attached property reaches a nested table**: `Views/Shed` sets
  its answer outright.
- **A figure wider than its cell is silently ellipsized.** Test long figures.
- **A record in a list is named to UI Automation by `ToString()`.** Text in a
  control's template is not a control to UI Automation (`Views/Pane` has its own
  peer). A custom peer must not claim to be a control unconditionally.
- **Tabular figures need `TextFormatter`** (`DrawnText`), not `FormattedText`.
- **`DrawingVisual` layers are hit only where they have ink**: draw a
  transparent rectangle first.
- **`AffectsRender` redraws a chart nobody can see, and WPF lays out text it
  isn't showing.** Guard on `IsVisible` / `Seen`.
- **An effect is recomputed whenever anything in or under it is drawn.** Keep
  effects off anything that moves with the clock; shadows on live charts are
  plain shapes.
- **`DispatcherTimer` can't go faster than ~16/s**; use `Native/Metronome`.
- **At 150 %** a one-unit line blurs and rows round up to whole pixels
  (`PanelFit` works in pixels).
- **Naming a part `Left`/`Top`/`Width`/`Height`, or a method `Drop`/`Name`,
  hides a base member** (CS0108 → build error).
- **Input:** an arrow key a control doesn't use moves focus away (a control
  using arrows keeps all four); `CaptureMouse` raises a mouse move before it
  returns (mark a drag begun first); a `Thumb` takes focus when pressed and
  its drag numbers are relative to itself.
- **Focus, then `UpdateLayout`, then `BringIntoView`**: a scroll viewer keeps
  only the later of two requests.
- **Stretched content with a `MaxWidth` is centred**; use a `Grid` column with
  a `MaxWidth` for "left, up to so wide".
- **A resource dictionary change walks every element in the window**: swap in
  one step.
- **Settings JSON:** a property whose shape may be wrong is a `JsonElement?`;
  computed properties need `[JsonIgnore]`; the relaxed encoder keeps "+".
- **`TASKDIALOGCONFIG` is byte-packed** (160 bytes on x64); dispatcher timers
  don't fire while a task dialog is open.

**Panels and the shell**
- **`DragMove` or answering a hit test as caption/edge activates the window**
  despite `WS_EX_NOACTIVATE`: hence `Overlay.Drag`. If the capture fails, drop
  the drag.
- **A panel's sizing strip is inside it** (6 units); a scroll bar's thumb
  outranks it (`PanelWindow.EdgeUnder`).
- **Windows may hand the foreground to a panel** when Zerg's foreground window
  goes away; `MainWindow.HandOn` takes it back and logs it.
- **A tray menu must be made foreground** (`TrayIcon.Front`), allowed only in
  answer to a real click. A second launch must call `AllowSetForegroundWindow`
  before signalling the first.
- **A hidden main window is not the process's main window.**

**Scratch programs and this machine**
- **An `Application` starts itself**: don't make a `Zerg.App` in a scratch
  program; parse `App.xaml`'s resources into a plain `Application` instead.
- **View models save `settings.json`** on many property sets, some 400 ms later
  from a `Debouncer`: give values through the `Settings` object, stop the
  timers, keep slider values in range.
- **A `Window` never shown lays nothing out**; but a bare `HwndSource` stage
  doesn't follow dictionary swaps, so use an unshown `Window` for that.
- **.NET 10 file-based apps** have reflection JSON off: use `JsonObject`.
- **A background `Zerg.exe` in a pipeline keeps it open**: redirect output.
- **A fault before the main window is up ends Zerg** (exit code 1). An old build
  that failed to start may linger windowless and hold the mutex: check the
  process list.
- **Running the dev build while source is being edited** can catch the tree
  half-changed.

## Status and what's next

- **Current: 1.2.0** (2026-10-21): Replay, an addon event file read whole
  into a View tab, cropped to a range and exported as a parse. 1.1.0 compared
  two different actions side by side; 1.0.0 was the first version for
  distribution, with an installer (`Zerg-<version>-setup.exe`) beside the zip.
  `dist/Zerg` is 1.2.0 with the Unreleased work (2026-10-26).
- **Packaging** works (`tools/package.py`: zip and installer). The installer is
  per user (`%LOCALAPPDATA%\Programs\Zerg`, HKCU uninstall entry), ticks the
  desktop shortcut and the `.zerg` association by default, and its finish page
  links VibeXI (that page's box is ticked unless `%LOCALAPPDATA%\VibeXI\events`
  exists). Uninstall asks about `%LOCALAPPDATA%\VibeXI\zerg` (No by default) and
  removes the association only while it opens the uninstalled exe. Testing it
  writes the user's HKCU `.zerg` keys: save and restore them. Not done: a start
  on a clean machine (no .NET, no VibeXI folder), signing (SmartScreen warns;
  the README explains), a place to publish the downloads.
- **Never tested:** a saved main-window placement on a second monitor of
  another scale; most of the UI at 100 % scale; the light theme for the newer
  features (side tables, View tabs).
- **Known loose ends:** three pairs of Damage/Healing cards are still two XAML
  files each (only the drill-down was unified); a new hit arriving at the top of
  a kept list in a live session has not been seen. In a body ~700 units tall the
  Damage section's right column is tight with a drill-down open.
- **Open ideas:** signing; a one-device-pixel hairline; lists that scroll with
  the keyboard; a folded pane's short note; the Snap Layouts flyout on maximize.
  (A reset for a pop-out's own opacity was offered and declined.)
