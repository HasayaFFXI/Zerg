# Zerg — changelog

What each version of Zerg was, newest first. The number is in
`Directory.Build.props`; `CLAUDE.md`, "Versions", says when it moves and how
a version is cut. Work that has no version yet goes under "Unreleased" and
takes its number when the version is cut.

## Unreleased

- The mouse wheel scrolls a wide table under the pointer (the Overview,
  Actions, Healing by character, Heals, Compare's tables). The sideways
  scroller round the table took the wheel and handed it to the page, so
  the rows never moved under the wheel, only by their scroll bar.
- Zerg says when the event file on screen was written by a VibeXI addon
  older than the one it expects (2.0.0): "VibeXI addon version is behind
  expected version: 2.0.0, data may be inaccurate or missing", in an amber
  strip under the command bar and again on the status line, for as long as
  that file is on screen. The version is the one the file's first line
  states; a file that states none counts as older, a newer addon does not.
  The followed file and a replay's tab are checked; a saved parse is not.
- The Overview's rows fold instead of isolating. By character, pressing a
  character's row shows the targets they dealt damage to under it, each
  with their figures at it and its share of their damage; By target,
  pressing a target's row shows its characters, as its arrow did. Every
  row has a tick box, which isolates: a heading's box a character (or a
  target) as a press on the row used to, a box under one that character
  at that target. A heading's box is half filled while only some of
  what is under it counts. The boxes set the Character and Target
  filters, so their menus and the Filters pane show the same; the
  pane's note says what is isolated, and its Clear lets both go.
- The command bar's filters come first: Character, Target and Type, then a
  rule, then Include Skillchains, Hide names and Click-through. A narrow
  window wraps the two groups apart.
- A table's columns can be dragged to another place by their headings, in
  the Overview, Actions, the Filters pane's two tabs, the Battle Log,
  Healing by character, Heals and Support. A grouped column (Ranged,
  Weaponskill, Skillchain, Pet) carries its whole group, as dragging the
  group's name does; the first column stays first. A press without a drag
  still sorts. Each of those panes has a Columns menu listing every column
  in the order they stand, a group's columns under its name, which shows
  or hides them all (a dash while only some are shown), and Restore
  default columns. Each table's order and hidden columns are saved
  (settings.json `columns`); the Overview's hidden groups from before are
  kept. A floating panel shows the same order, and is not dragged.

## 2.0.0 — 2026-11-04

- Damage by character is the Overview pane, with a switch in its heading
  for two tabs: By character, the table it was, and By target, the same
  table under each target's heading. A heading carries the party's
  figures against that target and how many of it were killed; pressing
  it isolates the target (as its line of the Filters pane's By target
  does), and its arrow shows or puts away the characters under it; every
  target starts with them put away. Each character's
  row under a target is their damage to it, their Damage % their share
  of what it took; pressing one isolates them. No DPS or deaths there.
  Whichever tab was last shown is kept (`overviewBy` in the settings).
  Floating, the pane is the characters' strip, as before.
- The band of figures over the Damage section reads Elapsed, Total damage,
  Party DPS, Kills / Deaths, Top DPS, Biggest hit; over the Healing
  section, in the same way, Elapsed, Total healing, Party HPS, Kills /
  Deaths, Top healer, Biggest heal.
- The Damage section has a new installed layout, live and in a View tab
  alike: the cumulative chart across the top with the Battle Log beside
  it, a third of the page tall; under them the Overview over
  Actions on the left, Filters over the drill-down on the right. A layout
  the player has arranged is kept; Reset layout gives the new one.
- The Fights pane is the Battle Log, in the pane's heading, its floating
  panel and the tray's Panels menu (where it was missing).
- By target and By damage type are one pane, Filters, with a switch in its
  heading for the two tabs: By target as installed, and whichever was last
  shown is kept (`filtersBy` in the settings). The note and the Clear link
  are the shown tab's; while the other tab's filter has something
  isolated, that tab has an amber dot. The pane floats as one panel, tabs
  and all, its bar naming what both filters isolate. A layout saved before
  has Filters where By damage type stood (or By target, whichever came
  first), folded if that was; the two old panels' places and opacities are
  not carried over. The Compare section's two tables are unchanged.
- Damage by character has a Columns menu in its heading: Ranged,
  Weaponskill, Skillchain and Pet, each ticked while its group of columns
  is shown. A group unticked is gone from the headings, the rows and the
  Party line, and the columns left share its room, so a narrow pane drops
  columns later. Kept between runs (`hiddenColumns` in the settings). Not
  in the floating strip, which has none of the groups.
- Damage by character has a Crit % column right of Accuracy: the
  character's own auto-attacks (Attack, Offhand Attack) that crit, out of
  those that dealt damage (a swing into shadows is left out; weaponskills,
  abilities and the pet are not in it). Its Party line is the party's
  swings together; it sorts, and goes with Accuracy in a narrow pane. The
  floating strip has it too, as CRIT% beside ACC%.
- The Overview has a Delay column right of Crit %: the average time from
  one round of a character's auto-attacks to their next, in seconds to the
  millisecond (4.812). A round is one packet, so a double attack or an
  offhand is not a round of its own. Weaponskills, job abilities, spells
  and ranged attacks between two rounds are part of it. A sample is not
  counted when the first round's target died before the next, the two
  rounds were at different targets, the character died or the session was
  paused between them, or it is far longer than that character's usual
  (more than twice it, and once more for each action between). By target
  gives each character their delay at that target; target headings and
  the Party line have none. It sorts, and goes with Accuracy and Crit % in
  a narrow pane. The floating strip has it as DELAY beside CRIT%. RULES.md,
  "Delay", has every rule.
- Accuracy is auto-attacks alone: ranged attacks are no longer in it, in
  Damage by character, the floating strip, By target's Acc or Compare's
  Accuracy. They have a Ranged group of their own in Damage by character,
  right of Crit %: Acc (with the low accuracy mark) and Crit %, counted as
  Accuracy and Crit % are, a dash for anyone who never shot. It goes first
  in a narrow pane, with Skillchain, Pet and Deaths. The floating strip
  has them as R.ACC% and R.CRIT%.
- The band of character buttons under the command bar (Characters, All,
  None) is gone, and characters are no longer left out: they are
  isolated, as targets are. Pressing a row of Damage by character counts
  only that character's damage; another press adds one, pressing an
  isolated row lets it go, and the pane's Clear link empties it. The
  others stay listed with their own figures, dimmed; the band's total
  reads "Damage by Hasaya" with its share of everyone's. A Character menu
  beside Target does the same, with each character's damage and share.
  Healing by character does the same for the Healing section, apart from
  the damage ("Healing by Sylviane"). Isolating moves no clock. It is not
  saved: Start and a new event file empty it, and each parse in View keeps
  its own. A floating panel's strip dims the others but is not pressed.
  The old `excluded` and `charactersOpen` settings are no longer read.
- Times are milliseconds, as VibeXI writes them from its 2.0.0 on, and an
  older Zerg cannot read them. An event file from an older addon (whole
  seconds) is not read: its lines are counted as unreadable, a replay of
  it says "invalid format, check VibeXI version", and the Replay menu
  gives it no times. The session runs from its first
  counted row to its last, exactly: Start arms at the press itself (not
  the start of its second), and Pause ends the clock at the last party
  row (not the end of that row's second), so a pull reads a little shorter
  and its DPS a little higher than before. A pull of one swing has no
  length, and its DPS and HPS are a dash. A replay's range holds the rows
  at its ends. The Replay menu's times leave out the meta line's (when the
  addon loaded).
- Parses are format version 2, their times in milliseconds. A parse of
  version 1 is refused, as is one missing any part of its session (armed,
  started, paused, the pauses) or its kinds or jobs.
- Nothing from before the addon's 2.0 is read any more. A kill is matched
  to its target by id alone: a row or death line without one is no fight's
  (a parse saved before ids shows no kills). Compare reads 0 healing and 0
  kills where a run has none, instead of dashes.
- A file that cannot be opened as a parse, for whatever reason, is refused
  with one message: "Unable to load parse: invalid format, check VibeXI
  version." (in View and in Compare; why goes to the log).
- Times that measure something read to the tenth of a second, cut, never
  rounded up: the Elapsed clock over Damage and Healing, its tenths drawn
  smaller (00:23.9), and the floating panel's clock the same way; kill
  times in the Battle Log (with its average, fastest and slowest), By
  target and the band's "killed in"; the drill-down's list of hits
  (0:23.8, its time column a little wider so 1:02:07.4 fits); Compare's
  Length, Avg kill time and kill times by target, a change in time under a
  minute in tenths too (−47.5s); a parse's or replay's length. Times of
  day, the Battle Log's When, the charts' axes and readouts, the tray menu
  and the replay range stay in whole seconds. A panel's text halo is now on
  each part of its bar rather than the whole bar, so the clock's tenths
  redraw only the clock's.

## 1.7.0 — 2026-11-02

- Kills and deaths, from the addon's new death lines. The band over Damage
  and Healing has a Kills / Deaths cell: "14 / 3", deaths in red, and the
  kills an hour under it. Damage by character has a Deaths column at its
  right, red when not 0, its Party line the total, and the floating strip
  has it too. A new Fights pane, between Damage by character and Actions,
  lists every kill newest first: when, the monster with its code as XIUI's
  target bar shows it ("Tiger [00B]"), its kill time, the damage and the
  DPS over that fight, with a line for each party death between them.
  Pressing a fight isolates it, alone or beside targets picked with the
  Target button; the band then reads "killed in 0:42". By target names
  each monster with its kills, "Tiger (10)", and has a Kill time column.
  The cumulative charts mark a death with a red X on that character's line,
  dashed until they next act, and tick the time axis for kills (faint) and
  deaths (red), with both in the hover card. A replay's range strip ticks
  them too, and its band says how many the range holds. Compare adds Kills
  per hour, Avg kill time and Deaths, and By target "Tiger (9 · 10)" with
  each run's kill time. A layout saved before gains the Fights pane over
  Actions. Needs the addon that writes death lines and target ids; a file
  from before has no kills (dashes in Compare), and one with death lines
  but no ids matches fights by name.

## 1.6.0 — 2026-11-02

- Enfeebles: a Sleep, Slow, Stun, Dispel, Lullaby, Light Shot or a pact's
  Nightmare is no longer counted as damage. One that was resisted or had no
  effect was read as a magic attack that missed: it was listed in Actions
  at 0 % accuracy, could start the clock, and one that landed was not in the
  file at all. The addon now writes every enfeeble on a monster as a line of
  its own, landed or not, and the Support pane lists them: a Buffs and
  Enfeebles switch in its heading picks which. Enfeebles are counted as buffs
  are: casts by character and action, and under an action the monsters it
  reached, how many it took on (Hit) and how many resisted it or it had no
  effect on, an already sleeping one say (Miss). They start and end no
  clock and are in no damage figure. The switch is saved. Needs the addon
  that writes them; an event file from before it still has the resisted
  ones as misses.
- Healing: a pet's heals are now its owner's healing, as a pet's damage is
  their damage. A dragoon whose wyvern did the healing had 0 under Healing,
  no share, no line on the chart and no HPS, with the figure only under Pet
  Healing. Now Healing, Healing %, HPS, Casts, Avg / cast, the top healer,
  the healing total and the cumulative chart all count it, in Compare too;
  Pet Healing still says how much of it was the pet's. In the Heals pane a
  character's heading reads "incl. 420 pet" where it read "+ 420 pet".

## 1.5.0 — 2026-10-30

- Heals: three tabs in the pane's heading list the heals three lines deep.
  By spell, as before, is each character's heals, and pressing a heal lists
  the people it reached under it. By caster is each character's people
  healed, and pressing a person lists the heals that reached them, a pet's
  among them under its name. By target is everyone healed, with everything
  that reached them (pets' heals included) and its share of all healing;
  pressing a person lists who healed them, and pressing a character lists
  their heals on that person. Pressing a line opens the drill-down too, and
  pressing it again puts both away. A person's drill-down is every cast that
  reached them, each what it healed them; a line a step further in drills
  on that one heal on that one person. A person's Casts are the casts that
  reached them, and a third line's Share is out of the line it is under.
  The tab is saved, and each keeps its own drill-down and open lines.
- Healing section: as installed, Support stands at the top of the right
  column and Cumulative healing under it, over the drill-down. A layout
  already rearranged keeps its own until it is reset.

## 1.4.0 — 2026-10-30

- Support: buffs, status removals and wards (Protect, Haste, Erase, an
  avatar's ward) are no longer counted as damage. Anything one of the party
  did to one of the party that was not a heal was read as a magic attack
  that missed: it could start the clock and hold it open, and it was
  listed in Actions as a spell that missed. An older addon's Protect and
  Shell even counted 40 damage each. They now have a pane of their own in
  the Healing section, Support, which lists how many times each character
  cast each one, and nothing else: they are not healing either, so they are
  in no total, average or rate. They start no clock and end none, and are
  counted over the same session as heals. Support can float like the other
  cards. Parses saved before this open with their buffs moved to Support.
  Beside Casts, Hit and Miss say on how many of the people reached it
  took effect and on how many it had none (an Erase that found nothing,
  a Protect on someone with a stronger one), and Hit % is the share it
  took on. In a narrow pane Miss and Hit % go. Pressing an action (or
  Enter or Space on it) lists everyone it reached under it, pets too,
  each with the casts that reached them, their hits, misses and Hit %;
  pressing it again puts them away.
  The count is as complete as the addon that wrote the file: an addon
  before the one that writes support which took effect wrote only the
  casts that had none (an Erase that found nothing to remove).
- The Actions table has an Acc column after Miss: each action's accuracy,
  counted as a character's Accuracy is (a swing into shadows got through,
  a weaponskill only if it dealt damage), with the low accuracy mark. A
  character's heading now reads their Hits and Miss over all their
  actions. Acc sorts, and goes with Miss and Min in a narrow pane. Share
  is the figure alone: the small bar beside it is gone.
- Zerg no longer looks for the addon's `meta` lines, which the addon has
  stopped writing. Event files from an older addon, which have them, read
  as before.

## 1.3.0 — 2026-10-26

- The By target pane has three more columns: Acc, the party's melee and
  ranged accuracy at each target; WS Avg, the average of the weaponskills
  that dealt it damage; and WS %, the part of what it took that came from
  weaponskills. Each reads what the Party line reads with that target
  isolated. Acc has the low accuracy mark, as a character's Accuracy does,
  at the threshold set on the Settings page. In a pane too narrow for them
  they go, and the table is the three columns it was.
- Columns sort. Press a column heading in Damage by character, Healing by
  character, Actions, Heals, By target, By damage type or a drill-down's
  list of hits: largest first (names A to Z), again for the other way, a
  third time for the table's usual order. A caret beside the heading says
  which column and which way; the headings take the keyboard (Enter or
  Space). Dashes go last either way, and rows that come out equal keep
  their usual order. In Actions and Heals the characters stay largest
  first and their actions or heals are sorted under them; By target keeps
  the line for unnamed damage last; a drill-down's Damage and vs avg sort
  its hits, newest first being its usual order. A floating panel lists its
  rows as the docked table does. Each table's order is kept in
  settings.json (`sorts`).

## 1.2.0 — 2026-10-21

- Replay: an addon event file can be read whole into a View tab of its
  own, cropped to the stretch wanted and exported as an ordinary parse.
  Replay on the command bar lists the newest files in the events folder
  (whose, when, how long, how big) and Browse for any other; Replay on the
  empty View section, or a `.jsonl` dropped or opened there, does the same.
  The tab is marked with a play sign and its clock tag reads REPLAY. Under
  its band a range band shows the party's damage over the whole file, with
  two handles to drag (or the times typed, or Left and Right on the
  keyboard), and Whole file to take all of it. Everything below counts
  only the range, its clock started and ended as Start and Pause would
  have. Export range… saves the range as a `.zerg`. A file with no party
  damage is refused.

## 1.1.0 — 2026-10-18

- Zerg is free software under the GNU General Public License, version 3
  (`LICENSE`). The zip and the installer carry it as
  `LICENSE.txt` beside `Zerg.exe` and `README.txt`, and setup shows it on a
  page of its own, to read before going on (nothing to accept).
- Zerg starts dark unless told otherwise: the installed theme is Dark, not
  System. A settings file that already names a theme keeps it; one with no
  theme, or a theme Zerg doesn't know, gets Dark.
- Two different actions can be compared, the same character's or two
  characters'. In the Damage and Healing sections (and a View tab), Ctrl
  and a press on a second action's row, or the drill-down heading's new
  pick button, sets it beside the open one: the drill-down becomes the two
  side by side, A and B, with both on one chart on shared bins, a table of
  their figures with the change B against A, and every hit tagged with its
  side. A is the character's colour, B the other character's or A's next
  shade; Swap trades them. In Compare an opened action has a pick button
  for each run's side: choosing another action for B (any character's) or
  A makes a pair, a row of its own at the top of that row's actions
  ("Tachi: Shoha ⇄ Tachi: Jinpu"), opened onto the two runs' spread, with
  Unpair to take it away. Swap A ↔ B carries the pairs over.

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
