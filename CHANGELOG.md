# Zerg — changelog

What each version of Zerg was, newest first. The number is in
`Directory.Build.props`; `CLAUDE.md`, "Versions", says when it moves and how
a version is cut. Work that has no version yet goes under "Unreleased" and
takes its number when the version is cut.

## Unreleased

- Zerg is free software under the GNU General Public License, version 3
  (`LICENSE`). The zip and the installer carry it as
  `LICENSE.txt` beside `Zerg.exe` and `README.txt`, and setup shows it on a
  page of its own, to read before going on (nothing to accept).
- Zerg starts dark unless told otherwise: the installed theme is Dark, not
  System. A settings file that already names a theme keeps it; one with no
  theme, or a theme Zerg doesn't know, gets Dark.

## 1.0.0 — 2026-10-18

- Zerg comes with an installer, `Zerg-<version>-setup.exe`, beside the
  zip. It installs for the signed-in user alone, with no administrator
  asked for, in `%LOCALAPPDATA%\Programs\Zerg` unless another folder is
  picked. It makes a Start menu shortcut, and offers a desktop shortcut
  and opening `.zerg` files with Zerg, both ticked. Its last page says the
  VibeXI addon is needed and links to it. Zerg is uninstalled from
  Windows' Installed apps, which asks whether to remove the settings and
  logs too (no by default) and takes back the `.zerg` association while
  it opens that Zerg. `tools/package.py` makes both downloads.
- Damage by character narrows with its pane, as By damage type does,
  instead of scrolling sideways under 900 units. Its columns give up room
  down to what their figures need; past that the table drops columns a
  group at a time from the right: Pet and Skillchain first, then
  Weaponskill and Accuracy, leaving Character, Job, Damage, Damage % and
  DPS. It scrolls only in a pane too narrow even for those five.
- The Settings page's samples name their second character PlayerB.

## 0.3.0 — 2026-10-14

- A `.zerg` parse opens from Explorer with a double-click, in the View
  section, and such files carry Zerg's icon, once "Open .zerg files with
  Zerg" is pressed on the Settings page (Parse files). A Zerg already
  running opens the parse and comes forward. `Zerg.exe <file>.zerg` does
  the same from a command line.
- More than one parse can be open at once. Each parse opened (Import, the
  View band's Open another…, a dropped file, a file from Explorer) gets a
  tab of its own in the title bar, named by the parse, with a mark that
  closes it; a click on a tab shows that parse. Opening a file that is
  already open brings its tab forward. Each tab keeps its own Damage |
  Healing side, and a new one opens on the side picked last. The band's
  Replace… is gone; its Close closes the tab on screen, and the tab beside
  it comes forward. When the tabs outgrow the title bar their names are
  cut short to fit. Each tab, and the session, keeps its own drill-downs
  and isolated targets and types: switching away and back finds them as
  they were left.
- Several `.zerg` files selected in Explorer and opened together (Enter,
  or Open) each open in a tab of their own, whether Zerg was running or
  not. A file that will not open is named in the View band. `Zerg.exe`
  takes several files on its command line too.
- A parse's tab can be dragged along the title bar to put the tabs in
  another order: the others step aside as it passes, and the tab dragged
  becomes the one shown. Esc while dragging puts it back. With a tab
  focused, Ctrl+Shift+Left and Right move it one place.

## 0.2.3 — 2026-10-14

- Zerg comes as a download for players: one `Zerg.exe` that needs no .NET
  installed, in a zip with a README (`tools/package.py` makes it).
- Parses saved by the browser build that came before Zerg (`.json`) no
  longer open, and the Open dialog offers `.zerg` only. A parse in which
  nobody healed still opens. An export no longer carries the unused
  `manual` key; a `.zerg` that has it opens as before.
- Figures follow one rounding rule (halves away from zero, on the value):
  1.005 is 1.00 to two places, and nothing prints as "-0".
- An event line whose fields are of the wrong JSON type is skipped and
  counted ("· 3 unreadable" on the status line), never read as damage.
- Compare colours a change only when its printed figure moved ("±0.0%" is
  flat).
- Faster counting in long sessions; the drill-down's hit list keeps its
  scroll position as hits arrive; a hand-edited settings file loses only the
  value that is wrong.

## 0.2.2 — 2026-10-09

- Two more panes in the Damage section, and so in the View section's
  damage side: By damage type and By target, what each type came to and
  what each target took, with its share. A row is pressed to isolate what
  it names, as its line of the Type or Target button's menu does, and the
  two stay in step; the rows that are not isolated are dimmed, and the
  pane's heading has Clear. Both can be popped out, folded and moved like
  any pane. They stand in the right-hand column, under the chart and over
  the drill-down.
- An arrangement of the Damage section saved before this keeps everything
  in it and gains the two panes beside its drill-down. More generally, a
  pane a saved arrangement lacks is now put beside the pane it is
  installed beside, not under everything else.

## 0.2.1 — 2026-10-08

- A Target button, on the command bar over the Damage section and beside
  Include Skillchains in Compare: counts only the damage dealt to the
  targets picked from its menu, one or several, listed alphabetically. In
  Compare a row of the By target table can be pressed to the same end. What
  is picked is not saved, and Start clears it.
- A Type button beside it, in both places, and in Compare the rows of the
  By damage type table pressed the same way: counts only the damage of the
  types picked. The two filters work together, and each lists what the
  other leaves.
- Two counting rules added: isolating targets, with no DPS while one is
  isolated, and isolating damage types (`RULES.md`).

## 0.2.0 — 2026-10-08

The redesign of the UI: every colour and control is Zerg's own.

- A title bar of Zerg's own with the sections as tabs, and one command bar.
- A band of five figures with small marks and the state tag; the characters
  as one line of chips.
- Each section is four flush panes that the player resizes, folds and
  moves; each section's arrangement is remembered, and can be locked.
- Dense tables in which the row is the bar, with grouped headings, a mark
  on low accuracy and a Party line.
- Charts that name their lines at their ends.
- Compare as bands over panes, its rows shaded in the two runs' colours,
  which are settings.
- A Settings page of ruled rows, with a colour picker.
- Panels whose bars are all figures until the pointer is over them.
- New settings: `shadeCharacters`, `shadeActions`, `lowAccuracy`, `runA`,
  `runB`, `layouts`, `layoutLocked`.
- One counting rule added: the Party line (`RULES.md`).
- The version, at the right-hand end of the status line.

## 0.1.0 — 2026-10-05

The first native build: a Windows app in place of the browser build, reading
the event files the VibeXI addon writes. Damage, Healing and Compare, a
Settings page, and cards that float over the game as see-through panels.
