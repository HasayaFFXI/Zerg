# Zerg — the event contract and the counting rules

What Zerg reads, what it counts, and why each rule is the way it is. The code
is in `src/Zerg.Core`; each rule names the function that holds it, and that
function's own comment has the detail. Read this before changing any number
Zerg prints.

Many rules exist so that a figure here agrees with **the reference parser**:
the in-game parser most players run beside Zerg. The addon copies that
parser's id tables (`addons/VibeXI/vx_enums.lua` in the VibeXI repository names it and says which
table came from where). Agreeing with it is how a figure in Zerg gets checked,
so a rule marked "as the reference parser counts it" is copied, not designed,
and is changed only by measuring against the game.

## One source: the addon's event file

`addons/VibeXI` reads the game's own action packets and appends one JSON object
per line to `%LOCALAPPDATA%\VibeXI\events\<Character>_<date>.jsonl`. That file
is Zerg's only input. There is no chat-log reader and there must not be one:
everything a chat log makes a reader guess (who is a player, which lines are
one area attack, crits, misses against parries, a pet's owner) the packet
states outright, and a second source that disagrees with the first is worse
than one that is right.

Reading the file (`EventFiles`, `EventTail`):

- **The newest `*.jsonl` is followed.** A newer file appearing is a switch:
  state resets and the session goes idle, since a clock still running from
  another character or another day measures a session that is not this one.
- **Byte offsets, not line counts.** A read stops at the last newline and
  reports the next offset, so a half-written line waits for the next poll
  instead of being parsed as a broken one. The addon flushes per event; half a
  JSON object is not parseable.
- **The file is opened sharing read, write and delete.** The addon writes it
  from inside the game, on the game's thread, and must never be blocked.
- **The file is ASCII.** The addon escapes every byte from 0x7F up, so there
  is no encoding to negotiate. A mangled name is fixed in the addon.
- **A file that got shorter is read again from the top.**

## The event contract

One line is one `(action, target, result)` row. A field of the wrong JSON type (a string where a number goes, a number that is not finite, a time no clock can show) makes the line unreadable: it is skipped and counted, never coerced. A missing or null field is its type's empty value:

```json
{"t":1785000000000,"seq":3,"use":41207,"kind":"ws","actor":"Player","actorKind":"player",
 "action":"Tachi: Jinpu","actionId":32,"target":"Goblin Pathfinder","targetKind":"mob",
 "dmg":723,"hit":true,"crit":false,"burst":false,"msg":185}
```

- `kind` is one of `melee ranged ws magic ability mobtp pet skillchain addl
  reaction`. `msg` is the game's raw message id, on every row, so new outcomes
  can be read later without changing the format. `owner` and `pet` appear only
  on a pet's own rows.
- `targetId` is on a row aimed at a monster, from the addon that writes it:
  the monster's id, a spawn point (see "Kills and deaths").
- **`t` is Unix milliseconds**, the PC's clock when the packet arrived, with
  `seq` ordering the lines that share one (every line of one packet does).
  Everything in Zerg is in milliseconds, so it is read as it is. An addon
  before 2.0.0 wrote whole seconds: a hard cutover, not two formats (the
  addon's fallback, a clock that can only give whole seconds, still writes
  milliseconds, ending `000`).
- **A time in the old format's seconds makes its line unreadable**
  (`EventRecord.InSeconds`): 10⁹ up to 10¹², which as seconds is 2001 on
  and as milliseconds January 1970 to September 2001, when no addon wrote.
  Read as milliseconds such a file would be counted, dated 1970; refused,
  its lines are counted as unreadable on the status line, a replay of it
  says "invalid format, check VibeXI version", and the Replay menu gives
  it no times. Smaller times are left alone: no addon writes them either,
  but neither are they a misread format.
- The generator `tools/gen-test-events.py` must stay field for field what
  `addons/VibeXI/vx_emit.lua` writes. Diff against `vx_emit.encode` when
  either changes.

Three kinds of line are not rows, and `EventReader` files each elsewhere:

- **`kind:"job"`**: one party member's jobs. It goes on the roster, never in
  the rows: a zero-damage row would land in every count.

  ```json
  {"kind":"job","t":1785000000000,"actor":"Player","main":"SAM","mainId":12,"mainLvl":75,
   "sub":"WAR","subId":1,"subLvl":37}
  ```

  - Written on change, not on a timer, so `EventReader.Reset` keeps the
    roster. A cleared job map would stay empty until somebody changed job.
  - Last write wins (a second job line is a real change). Spawn kinds are the
    opposite, first answer wins, because a spawn kind cannot change.
  - The `sub` trio is left out when there is no sub job.
  - It also marks the name as one of ours. For a member who never acts it is
    the only evidence there is.
- **`kind:"heal"`**: one per target of a heal, with `hp` and never `dmg` or
  `hit`, so a heal mistaken for a row would still add nothing.

  ```json
  {"kind":"heal","t":1785000000000,"seq":2,"use":7,"via":"magic","actor":"Catpirate",
   "actorKind":"player","action":"Curaga II","actionId":8,"target":"Player",
   "targetKind":"player","hp":190,"msg":367}
  ```

  Which actions are heals is the addon's decision, by action id, as the
  reference parser decides it. `via` is `magic`, `ability` or `pet`.
- **`kind:"enfeeble"`**: one per monster an enfeeble reached, with `hit`
  and never `dmg`, `crit` or `burst`.

  ```json
  {"kind":"enfeeble","t":1785000000000,"seq":4,"use":7,"via":"magic","actor":"Mage",
   "actorKind":"player","action":"Sleepga","actionId":273,"target":"Goblin Smithy",
   "targetKind":"mob","hit":true,"msg":236}
  ```

  Which actions are enfeebles is the addon's decision, by action id
  (`vx_enfeeble_ids.lua`: what can be aimed at an enemy and deals no
  damage, from the server's tables and scripts). An enfeeble is written
  only this way, never as an action line, landed or not: as one, a resisted
  Sleepga was a dmg-0 magic miss no reader could tell from a resisted
  Stone, and one that landed was not written at all. `hit` is whether it
  took on that monster; resisted, missed and no effect are all `false`.
  `use` is one per cast, shared by every monster it reached. `EventReader`
  files it in the damage row's shape (its kind the line's `via`) in its own
  list, `Enfeebles`. See "Support" below.

- **`kind:"death"`**: a monster the party killed, or one of the party who
  died (`EventReader.Deaths`). No `dmg`, `hit` or `use`.

  ```json
  {"kind":"death","t":1785000000000,"seq":1,"actor":"Player","actorKind":"player",
   "target":"Tiger","targetKind":"mob","targetId":17195010,"msg":6}
  {"kind":"death","t":1785000020000,"seq":1,"target":"Player","targetKind":"player","msg":97}
  ```

  A monster's names the party member who killed it (none with `msg` 20),
  which Zerg reads and does not show: kills are the party's. As a row it
  would be that member's miss, and could start the clock. A player's names
  only who died: nothing a monster does to the party is in the file. One
  with no `target` or no time breaks the contract. See "Kills and deaths".

One kind of row is not damage either, and `EventReader` files it apart:

- **Support: a row one of ours aimed at one of ours** (`EventReader.IsSupport`:
  both `actorKind` and `targetKind` are `player` or `pet`), and not a heal.
  Protect, Haste, Erase, Utsusemi on oneself, an avatar's ward. The addon
  writes them as ordinary rows with `dmg:0`: `hit:false` when the cast had
  no effect (`msg` 75, 323), and `hit:true` when it took effect (230 gains
  the effect, 83 and 341 a status removed, ...). Among the damage rows each
  would be a magic miss that started the clock. Asked of the row's own
  kinds, as it arrives, not of the roster. See "Support" below.

Any other line with no `actor` is no row: it is passed over and not counted
as unreadable. A file begins with a `kind:"meta"` line, the addon's version
and when it loaded (`{"kind":"meta","t":1785000000000,"version":"2.0.0"}`),
which is read that way, and its time is not a record's (`EventFiles.Times`
passes over it).

**The addon's version is the first line's** (`EventReader.Addon`), checked
against the one Zerg expects (`AddonVersion.Expected`, its own number, not
Zerg's). A newer addon is taken as it is. An older one, a file whose first
line is not a readable meta line (an addon from before it, or the old
format's seconds), a meta line with no `version`, and a version that is not
one to three whole numbers are all behind, and while that file is on screen
(the followed file, or a replay's tab) a strip under the command bar and the
status line say "VibeXI addon version is behind expected version: …, data
may be inaccurate or missing". The reason: an older addon writes less, or
writes it differently, and its file reads as a quieter fight rather than as
an error. Nothing is refused for it; the counting is unchanged. A saved
parse does not record its addon, so it is never warned about.

## `use` is per swing, not per action

The packet has two dimensions, and they mean different things:

- **Several targets, one result each**: an area attack. One use.
- **One target, several results**: a multi-attack round. Two or three real
  swings, each with its own outcome, each its own use.

The addon mints `use` from the result's position, so result 1 across every
target shares an id and result 2 takes the next. `Counting.Collapse` folds the
rows of one use together: damage sums, hit, crit and burst are "any", and a
use on several targets reports `"3 targets"`.

Folding the second case into the first makes a round that landed once and
missed once read as one hit and no miss. The swing count is what accuracy
divides by, so the symptom is a party that never misses.

**The invariant: collapsing moves counts, never damage.**

## Skillchains and additional effects

Both ride on the row that caused them, and each is written as a row of its own
with its own `use`. Sharing the weaponskill's id would fold the chain's damage
into the weaponskill and lose it as a row. A chain's action is
`Skillchain: <name>`. **Include Skillchains** off drops every `skillchain` row
in `Counting.Counted`; the SC columns then print a dash, not 0.

## Party damage only

`Counting.Counted` is the single place a row is dropped for a reason other
than time, and both `Counting.Filter` and `Counting.FirstCounted` go through
it. Two copies would let the clock start on a row that is then not drawn.

- **Monsters' rows are read and kept, and dropped on the actor side.** Anything
  not positively ours (`player` or `pet`) is treated as a monster, so an entity
  the addon could not resolve is left out rather than added. Under-counting a
  stranger beats crediting one. Monsters still appear as targets.
- **Pets are ours, credited to the owner** (`Counting.Credit`). A pet has no
  row or colour of its own. The action keeps the pet's name as a prefix
  (`Fluffikins: Big Scissors`), since pet and master both swing an "Attack"
  and one average over both would describe neither. It copies, so the list
  stays true to the file.
- **Reaction damage counts, and arrives with its ends swapped.** A counter, a
  spikes proc and a Retaliation ride on a packet the monster is the actor of.
  The addon writes them with the reacting character as the actor, as
  `kind:"reaction"`.
- The addon does not record a monster's own swings; a row with a monster
  as its actor is still dropped here, whoever wrote it.

## The per-character table

Every column but Job and DPS is one of the reference parser's columns, counted
its way, with percentages to one decimal. Every figure is rounded on its value, halves away from zero, and a figure that rounds to zero has no sign.

| Column | What it is |
|---|---|
| Damage | everything counted: pet, and skillchains when the switch is on |
| Damage % | share of the party total |
| Accuracy | own auto-attacks (`melee`: Attack, Offhand Attack) that connected / attempted |
| Crit % | own auto-attacks that crit / those that dealt damage |
| Ranged Acc, Ranged Crit % | the same two questions over own `ranged` rows alone |
| Delay | the average time from one round of own auto-attacks to the next, in seconds; see "Delay" |
| WS Damage, WS Avg, WS %, WS Acc | own `ws` rows; Avg over weaponskills that dealt damage; Acc is dealt damage / used |
| SC Damage, SC % | the chains this character closed; a dash with the switch off |
| Pet Damage, Pet Acc | rows with an `owner`; the pet's melee swings |

- **Accuracy is `Counting.Connects`, not `Hit`.** `Hit` asks whether damage
  landed, and feeds totals, averages and histograms. `Connects` asks whether
  the swing got through: a swing into shadows (31) is a hit, a melee swing that
  healed the target (373) is a hit, a melee Perfect Dodge (32) is not an
  attempt, and a weaponskill connects only if it dealt damage. The Actions
  table's Hits and Miss columns are damage counts, so a shadowed swing is a
  Miss there and a hit in Accuracy. Two questions, two answers.
- **Crit % divides by `Hit`, not by `Connects`.** A swing into shadows
  connects but deals nothing and cannot crit; counting it would lower a
  character's crit rate for every shadow the monster had. The swings are the
  same ones Accuracy is over (own auto-attacks; no ranged attack,
  weaponskill, ability or pet), folded per use, so a multi-hit round's
  swings are each their own.
- **Ranged attacks have columns of their own.** A ranged attack's hit and
  crit rates are another weapon's, with another stat behind each; folded
  into Accuracy, a character's one or two shots a fight would move a melee
  figure that has nothing to do with them. Ranged Acc and Ranged Crit % ask
  Accuracy's and Crit %'s questions of `ranged` rows (shadows, 31, a hit;
  crits are message 353, which the addon marks `crit`). A dash for anyone
  who never shot.
- **The Actions table's Acc is `Connects` over that action's uses.** A dash
  with nothing attempted (a melee Perfect Dodge is not an attempt). A
  character's heading has none: over every use of theirs it would be a
  third accuracy beside the per-character table's (auto-attacks alone).
  The heading's Hits and Miss are the rows under it together.
- **A pet's row is the owner's damage, never the owner's swing.** A pet's
  melee must not reach the owner's Accuracy, nor a pet's move WS.
- **Null means "nothing to measure" and prints as a dash.** A mage with no
  weaponskills has no WS Acc; 0% would say they missed every one.
- **Nobody at zero** in the table, the strip or the cumulative lines.
  Reaching the totals takes an action, not damage, and the Actions table
  shows what they did. One isolated stays listed though they dealt nothing,
  so the filter can be seen and let go.
- **The Party line is counted from the party's swings, not from the rows'
  percentages.** The line that closes the table prints the party's own
  Accuracy, weaponskill, skillchain and pet figures, worked out from every
  character's counts added together (`Aggregate.Party`, by `Split.Figures`,
  the one place a split column is worked out, for a row and for the party
  alike): three swings landed out of six is 50%, whatever the rows above
  say. An average of the rows' percentages would weigh a character who
  swung twice like one who swung two hundred times, and would not be the
  figure Compare prints for the same file (`Compare.Measure` already summed
  the counts). It is everyone counted, a character at zero included: their
  misses are the party's swings, though they have no row. The number under
  Job is the rows above it. A dash where nobody had anything to measure.
  The party's DPS there is the one in the band of figures, over the session
  clock.
- **One known difference from the reference parser:** it leaves melee
  additional effects out of its total and Zerg counts every `addl` row, so
  with enspells up Damage reads a little above it.

## Delay

How long a character takes from one round of auto-attacks to the next, on
average (`Delays`), shown in seconds to the millisecond. It is not the
weapon's delay: whatever the character did between two rounds (a
weaponskill, an ability, a spell, a ranged attack) is in it, animation and
all. That is time the weapon was not swinging, and the figure is meant to
show it.

- **A round is one packet**: every `melee` row of one character with the
  same `t`. A double or triple attack, an offhand, a kick attack and a
  Zanshin are rows of one round. Counted as swings they would put a sample
  of 0 between them.
- **A sample runs from one round to the next.** The first round only
  starts one: the file has no engage, and a sample from one would measure
  the walk to the monster. Each round ends a sample and starts the next.
- **Only the character's own rounds.** A pet's melee is never its owner's
  swing, as in Accuracy.
- **A sample is discarded**, and its second round still starts the next
  one, when:
  - **The first round's target died before the second**: its death line
    falls between the two, matched by id (as fights are). That includes
    the round that killed it, and a monster that respawned with the same
    id. The time after a kill is spent finding the next one.
  - **The two rounds were at different targets**: by id, or by name where
    a row has none. A switch, a monster that got away, one somebody else
    killed whose death line the file lacks. The same walk is in it.
  - **The character died between them.**
  - **Either round is outside the session, or a pause lies between
    them.** A pause comes off the clock, but no weapon stopped for it.
  - **It is longer than its ceiling.** The file never says when a
    character was asleep, stunned, out of range, disengaged and engaged
    again, or using an item, and a single such gap in twenty samples moves
    the average by a second. A sample may be up to twice the character's
    typical one, and once more of it for each of their own actions between
    the two rounds (a weaponskill starts the swing over). An action is a
    moment: an area weaponskill on three monsters is one. Typical is the
    lower median of their samples with no action between, or of all their
    samples where none is clean. The lower median, so that of two samples,
    one held up, the other is typical.
- **Paired before anything is isolated.** Rounds are paired over every row
  the reader holds, whatever the viewer's switches, as kills are. Paired
  after a target was isolated, a round at Kirin, a minute at the adds, and
  a round at Kirin again would be one sample a minute long. The ceiling's
  typical sample is the session's too, so a filter cannot change which
  samples count.
- **Then cut by the switches**: a sample is of the targets or the fight
  isolated when both its rounds are (they are at one target, so it is in
  or out whole). A dash while Melee is not among the types isolated, as
  Accuracy is. The characters isolated take nothing away: each row is its
  own character's figure.
- **The Party line has none.** One average over a great katana and a
  dagger describes nobody.
- **The Overview's By target tab** gives each character their samples at
  that target.
- **It starts and ends no clock**, and an export needs nothing new: its
  rows keep their times and ids, and its death lines go with it.

## The session clock

Every number is measured from the session's zero, and there is no other clock
(`Session`).

- **Start arms; the first counted row latches the zero**, at that row's own
  time. Pressing early costs nothing. A stopwatch that must be pressed on the
  frame of the first swing is always a second or two wrong, and every DPS is
  divided by that error.
- **Idle and armed both count nothing.** There is no zero to measure from.
- **What starts the clock is exactly what would be counted**: not a monster's
  swing, not a skillchain with the switch off. Whoever is isolated, any
  party member's swing starts it (see "Isolating characters").
- **A miss starts the clock.** Waiting for damage would put the misses before
  it outside the session and out of accuracy.
- **A heal does not, nor does support.** Only a counted damage row latches; a
  second definition of "counted" would be needed otherwise. A Protect cast
  before the pull is before the zero, and is not counted.
- **The arming time is the press, exactly**: the wire clock is the PC's, to
  the millisecond, so a swing stamped before the press came before it.
- **The latch is sticky.** A later filter change cannot re-date a running
  session. A filter change can latch an armed one, and that row is then the
  first thing counted, so it is the right zero.
- **Pause stops damage and clock together**, and a pause is subtracted, not
  skipped: two swings either side of a three-minute pause come out three
  minutes closer. Reading goes on during a pause; rows are dropped by their
  own timestamps, so poll latency cannot move one across the boundary.
- **A paused clock ends at the last party damage row**, not at the press
  (`Session.Snap`, `Counting.LastCounted`). Pause is pressed some time after
  the last swing, never on it, and that wait would be in every DPS the parse
  is remembered by. The end is that row's own time, and the row is counted:
  a pause holds what lies strictly between its ends, so a row in the very
  millisecond a clock stops is in (`Session.InPause`). The session runs from
  its first row to its last, exactly. A pull of one swing has no length, and
  its rates are a dash (`Counting.PerSecond`): an amount over no time is no
  rate, and nothing over no time is 0.
  - **What ends it is what starts it**: a miss does, a monster's swing and a
    heal do not. A heal after the last damage row is outside the session,
    and is not drawn or exported.
  - **But without the viewer's switches.** Any party row ends it, whoever
    is isolated and skillchain included. The zero is latched once and stays;
    an end worked out from the filters would move with each one, and an
    exported file carries no filters to work it out from again.
  - **Never later than the clock really stopped**: the press. Paused,
    resumed and paused again with nothing swung, the end is still the last
    row, before both pauses.
  - **The press and the pauses are kept as they happened**, and the end is
    worked out from the rows' own times on each read, so a row written
    before the press and polled after it moves the end. Resume puts the
    whole wait back on the clock: the session was not over after all.
  - **Screen, file and Compare read the one end.** An export is written as a
    session paused there. An import whose pause is later than its last row
    is snapped there when read, as a live session would be.
- **The second button is Pause or Cancel, never both.** Cancel calls off
  what has not happened yet: an arming, or a restart still being asked
  about. It never drops a measurement. A session with damage in it is ended
  by Start.
- **Start during a session re-arms**, and drops the rows read so far while
  keeping the read offset, the roster, the colour slots and the filters.
- **Over a started session, Start asks first**, in the pair itself and in no
  window: Start reads Confirm and the second button Cancel, on the main bar,
  every panel bar and the tray menu at once (`Tracker.First`). Start sits
  beside Pause, and reaching for one and pressing the other would drop the
  fight; the press that follows that slip lands on the second button, which
  is Cancel. The session is measured as before while the question is up.
  Unanswered for 5 seconds, the pull is kept and the pair is itself again,
  so Pause is never out of reach for long. A second press within half a
  second of the first is no answer: a double-click is one gesture. Idle or
  armed there is nothing to lose, and Start acts at once.
- **One denominator.** `Counting.Aggregate` divides the party's DPS and each
  character's by the session clock, so the column adds up to the party figure.
- **DPS decays between polls**: the total holds and the clock grows, so the
  elapsed time, the party DPS and each character's DPS are rewritten at the
  draw frequency (30 times a second unless set), together. A party figure
  decaying past a frozen column reads as a bug.
- **The chart's left edge is the zero and its right edge is the clock**, paused
  included. A flat line out to the present is a falling DPS, drawn. The axis
  and the DPS beside it must always describe the same span.
- **The session is not saved.** A restart returns to idle; a clock restored
  from an earlier run would be measuring time nobody was fighting.

## Isolating targets

The Target button counts only the damage dealt to the targets picked
(`Targets`, and one line of `Counting.Counted`). None picked is every target.

- **A target is its name.** Three Goblin Pathfinders are one target, as
  they are one line of Compare's By target table, "Goblin Pathfinder (3)"
  with the kills in brackets. One of them is one fight (below).
- **One fight can be isolated too**, from the Battle Log: the rows of
  that one life of that one monster (`Fight.Holds`), "Damage to Tiger
  [00B]". It takes the same rates away. Beside picked targets it adds to
  them: a row of either counts, so Mandragora and one Tiger fight are
  "Damage to 2 targets", every Mandragora and that one Tiger.
- **Several can be picked**, and the list they are picked from is
  alphabetical (`Targets.Sorted`), whatever their case. Not by damage: a list
  in that order moves under the pointer while a session counts.
- **The Damage section's By target table picks as well**, and is the same
  filter's: a row pressed is that target's line of the menu chosen. It
  lists the same targets with the same figures, largest first
  (`Targets.Ranked`), as Compare's table does and as a table of damage is
  read; whatever is picked that nothing was dealt to comes after, with
  dashes. Under them, where any row named no target, a line says what
  those came to (`Targets.Unnamed`): it is in the total, and there is
  nothing of it to pick.
- **By target's Acc, WS Avg and WS % are the Party line's with that target
  isolated** (`Counting.ByTarget`, through the same `Split.Figures`):
  the party's auto-attacks at it, its weaponskills' damage over
  those that dealt it damage, and that damage out of what it took. A row
  is asked its target before its use is folded, as isolating asks it, so
  an area weaponskill is one use at each target it reached, of what it
  dealt to that one, and one that dealt one of them nothing is a miss at
  that one. Of the types isolated, as the Damage column is; a dash where
  there is nothing to measure. By damage type has no such columns.
- **The Overview's By target tab is the per-character table under each
  target** (`TargetGroups`). A target's heading is its line of By target
  (the same total, the Party line's figures with it alone isolated) and
  is pressed to pick it, the same filter's. Under it each character's
  row is what their By character row reads with that target alone
  isolated, folded the same way, and is pressed to isolate them. A
  heading's Damage % is its share of what every target took; a
  character's is their share of what that target took, from everyone,
  isolated or not, so the rows under a target add to 100% and the row
  is shaded to their damage out of that target's leader's. It lists
  every target and every character whatever is picked, as By target and
  By character do; those not picked are dimmed. There is no DPS (a
  target's damage over the session clock is no rate anybody dealt, as
  below) and no deaths (a death is at no target).
- **The list names every target whatever is picked**, each with what it took
  from the characters who count (the characters isolated and the
  skillchain switch apply; the targets picked do not). A target nothing landed on is not
  listed, and neither are rows that name no target, which cannot be picked.
  A picked name stays listed, with dashes, while nothing that counts was
  dealt to it: a filter that could not be seen could not be taken off.
- **Asked of the row, before its use is folded.** An area attack that reached
  an isolated target and two others is one use that dealt what it dealt to
  that one: one hit, that damage, that target's name.
- **No rate is given while a target is isolated** (`Snapshot.Rate`). The
  clock is the session's. It ran while the party fought everything else, so
  damage to one target over it is no DPS anybody dealt, and the event file
  does not say how long a target was fought. A dash, not a number that reads
  low: Party DPS and its "running" mark, DPS in every row of the
  per-character table and on its Party line; in Compare, the Party DPS
  figure and its change, and DPS and its change in every row.
- **Kills an hour are the one rate that stays** (`Snapshot.KillRate`): of
  the targets isolated, over the session clock. A kill is a moment, not an
  amount spread over the fight, so how many fell in an hour of the session
  is as true of one target as of all. With one fight isolated there is one
  kill, and its kill time is given instead.
- **Everything else is counted as it was, of those rows alone**: totals,
  shares (of the isolated damage), accuracy (of the swings at those
  targets), the weaponskill, skillchain and pet columns, the biggest hit,
  the per 20 s bars and the cumulative lines, which are still drawn over the
  whole session clock and lie flat while the target was not being hit.
- **The clock does not move.** The zero and the end are the session's and are
  asked without the targets (`Counting.FirstCounted`, `LastCounted`), as the
  end is asked without the viewer's other switches. Elapsed, and Length in
  Compare, read as they did.
- **The total says what it is a total of**: "Damage to Kirin", "Damage to 3
  targets", with its share of the damage to every target under it.
- **Healing is never isolated.** A heal's target is who it healed.
- **The characters listed are of the targets isolated**, as the types
  are (see "Isolating characters").
- **What is picked is not a setting, and belongs to the parse it was
  picked in.** The session and each parse open in View keep their own
  while another is on screen, and get it back when they come on screen
  again: switching tabs to look at another parse is not a new question
  about this one. The session's is emptied by Start and by a new event
  file, a parse's when its tab is closed; in Compare, when a slot is given
  another parse or cleared. A filter kept for the next pull would count
  nothing, with nothing listed to untick. It is not in an export: filters
  are the viewer's.
- **Compare isolates in both runs at once**, by name, and has a filter of its
  own: its targets are two files', not the session's. By target goes on
  listing every target, largest first, with the isolated ones marked.

## Kills and deaths

From the death lines (`Kills`). A kill is the party's, never a character's:
who landed the killing blow is not shown.

- **A monster's id names a spawn point, not one life.** A monster that
  respawns comes back with the same id, so its death line is what splits
  one fight on it from the next. A fight is the party's rows at that id
  after its last death line (or from the start) up to its own.
- **One monster is shown with its code**, the last three hex digits of
  the id, uppercase, as XIUI's target bar shows them: 17195010 is
  `0x1066002`, "Tiger [002]". It is the monster's slot in its zone, so the
  same code comes back each time that spawn is killed. **Every monster of a
  name is shown with its kills**: "Tiger (10)". Ids are compared whole; the
  code is only shown.
- **A row or death line without an id is no fight's**: it is matched by
  id alone, never by name. Its damage still counts everywhere else.
- **Kill time is the first party row at it to its death line**, of the
  wall clock. The death line comes about a second after the killing blow;
  it is the only end the file states. A fight's DPS is its damage over its
  kill time: a clock that is the fight's own.
- **A kill is in the session when its killing blow is**: the last party
  row at it before its death line, and it is drawn there. Asked of every
  party row, whatever the viewer's switches, as the session's end is. The
  death line comes after that blow, so the last kill of a pull would
  otherwise fall after the end a paused clock is snapped to. With no party
  row at it, the death line's own time is asked. An export writes a kill's
  line with its blow, after the end if that is where it fell.
- **A fight's damage is what the characters who count dealt it**, as By
  target's is.
- **A party death is in the session when its time is**, as a heal is: one
  after the last party damage row is after the end. While characters are
  isolated, the band counts their deaths alone; the Deaths column is each
  character's own, isolated or not.
- **Nobody is shown as down.** The file says when someone died and nothing
  about when they were raised. What it does say is when they next did
  something of their own (a swing, a cast, a heal, a buff, an enfeeble;
  being healed is not acting), and the cumulative line is dashed from the
  death to there, or to the end where they have not.
- **Deaths start and end no clock**, and are no damage and no attempt.
- **The Overview's Deaths column** is each character's deaths in the
  session; the Party line's, everyone's. Only a player's death line is a
  death: a pet's is not its owner's.
- **Kills an hour** are over the session clock; see "Isolating targets".

## Isolating damage types

The Type button, beside the Target button over the Damage section and in
Compare, counts only the damage of the types picked (`DamageTypes`, and one
more line of `Counting.Counted`); in both, the rows of the By damage type
table pick as well. It is the targets' rule along another cut, and what is
said above of picking holds here: several, alphabetical by what each is
called, a list that names them all, kept and emptied with the targets. The Damage section's table lists the types in the order Compare's
has always had them (`DamageTypes.Listed`: melee, weaponskills,
skillchains, magic, ...), not by size: they are the same few in every
fight, and are found where they always are.

- **A type is a line of Compare's By damage type table**, in both sections. A row's type is its
  `kind`, but a pet's rows are the one type "Pet" whatever the pet did
  (`DamageTypes.Of`): the table has always summed them so, and a filter has
  to cut where the table does.
- **A type takes no rate away.** A type is dealt all through a fight, so its
  damage over the run's clock is a DPS somebody dealt: melee DPS, magic DPS.
  The dashes are the targets' alone.
- **Types and targets isolate together, and each list is of what the other
  leaves** (`Tracker.Count`, `Compare.Measure`). With Kirin isolated, the
  types listed are of the damage dealt to Kirin; with Melee isolated, the
  targets listed are of what melee dealt to each, and "All targets" is the
  melee total. Neither list is cut by its own filter, or there would be
  nothing left on it to pick.
- **A type's Share is of every type's damage**, isolated or not. The run's
  total is then only the isolated types', and a share of that would put the
  others over 100%.
- **Figures with nothing left to measure are dashes, as ever**: with
  Weaponskills isolated there is no melee swing to have an Accuracy of.
- **The total says both**: "Melee damage", "Damage of 2 types",
  "Weaponskills damage to Kirin" (`DamageTypes.Heading`).
- **With Include Skillchains off there is no Skillchains line**, and one that
  was picked stays on the menu with dashes, where it can be unticked.
- **Under the total, its share is of everything**: of the damage of every
  type to every target, whatever is isolated.

## Isolating characters

A press on a character's row of the Overview (either tab), or a tick on the Character menu
beside Target, counts only the damage that character dealt
(`Counting.Counts`, one line of `Counting.Counted`): in the band, the
cumulative chart, the Actions table, the Battle Log, By target and By damage
type. Another press adds a character; a press on an isolated row lets it go;
Clear empties it. None isolated is everyone. It works as a target does, for
the same reason: "what did these two do" is a question about part of the
fight, asked of the fight, not a change to what the party was.

- **Healing has its own**, pressed on Healing by character: the heals,
  the Heals pane, Support and the cumulative line are then the healers
  isolated. The two never cut each other. Who healed is not a question about
  who dealt damage, and a damage dealer isolated would leave the healing
  section empty. It has no menu: a party has two or three healers, and the
  table already shows every one.
- **Everyone stays in the table, with their own figures**; those not
  isolated are dimmed. A row that hid its figures could not be compared
  with, and one taken out could not be pressed back in. Damage % and
  Healing % are of everyone's, as a target's Share is of every target's.
- **The Party line is of those isolated**: how many, their total, their
  share of everyone's, and their Accuracy and rates over their own swings.
- **The total says whose it is**: "Damage by Hasaya", "Melee damage by 2
  characters to Kirin", "Healing by Sylviane", with its share of everyone's
  under it.
- **Each list is of what the others leave.** The characters listed are of
  the damage dealt to the targets and of the types isolated, and in the
  fight isolated; the targets and types listed are of the characters
  isolated. No list is cut by its own filter, or there would be nothing left
  on it to pick.
- **The clock does not move**, as with targets: the zero and the end are
  asked without the characters (`Counting.FirstCounted`, `LastCounted`). A
  filter set afterwards must not re-date a session, and one pull is as long
  whoever is looked at.
- **A rate stays a rate.** The characters isolated were in the fight all
  through it, so their damage over the session clock is a DPS they dealt.
- **Not a setting.** What is isolated belongs to the parse it was asked of,
  kept per parse and emptied by Start and a new event file, as targets are
  and for the same reason: one kept for the next pull would cut that pull's
  totals with nothing to say so but a dimmed row.
- **A floating panel's strip dims, and is not pressed.** A panel lies over
  the game and is often clicked through; a filter set by a stray click
  there would change every total on screen.

## Healing

`Healing.Filter` applies the session window as the damage filter does, and
the healers isolated (see "Isolating characters"), and credits a pet's heal
to its owner.

- **A pet's heals are its owner's healing**, as its damage is their damage:
  in Healing, Healing %, HPS, Casts, Avg, the top healer, the cumulative
  chart and Compare. Pet Healing is the pet's part of Healing, said apart.
  It once left them out, as the reference parser does, and a dragoon whose
  wyvern did all the healing read 0 under Healing beside a five-figure Pet
  Healing.
- **Casts are one per `use`**, however many targets; Avg is total / casts.
- **There is no overcure, on purpose.** The packet does not carry the target's
  missing HP, and an estimate against the spell's best cast so far was tried
  and removed as not accurate enough. Do not bring it back without a real
  source for missing HP.
- **A cure on a full target healed 0; it did not miss.** The cumulative line
  and the histogram reuse the damage code on a row-shaped copy
  (`Healing.AsEvents`) where every heal is a hit.
- HPS ticks with DPS, over the same clock.
- **The Party line under Healing by character** prints what `Healing.Totals`
  already has for the party: Healing, casts, total over casts, and how much
  of it the pets healed. Nothing new is counted for it.
- **The Heals table lists the heals three ways** (saved as `healsBy`),
  each three lines deep: By spell, character → heal → person reached; By
  caster, character → person healed → heal; By target, person healed →
  character → heal. All come from `Healing.Totals` (`HealAction.Targets`,
  `HealerTotals.Targets`, `HealTotals.Targets`).
  - **On a person, a cast is one that reached them**: a Curaga on five is a
    cast for each of the five, and Avg is what one of those casts healed
    that person. Min and Max are the same per-target figures as a heal's.
  - **A person's line holds the pet's heals on them too**, under the pet's
    name a line further in, so a character's people add up to everything
    under their heading, as their heals do. Share on a second line is out of
    that (the pet's included); on a third, out of the line it is under.
  - **By target, a person's heading is everything that reached them**, pets'
    heals included, and its share is of everything healed, so the headings
    add up to the party's Healing. Unlike a character's heading, the pets'
    part is not said apart: it is what they received,
    whoever's it was. A pet's heals are under its owner, by the pet's name.
  - **A drill-down on a person is of every cast that reached them**, each
    what it healed them alone (`Healing.Distribution` with a target): the
    same cast is one figure here and another, summed over all it reached, in
    the heal's own drill-down. One heal on one person is the same drill-down
    in every way, and so is everything one character cast on one person.

## Support

Buffs, status removals and wards (`Support`), listed in the Healing
section's Support pane. Enfeebles (`EventReader.Enfeebles`) are listed in
the same pane, by the Buffs / Enfeebles switch in its heading, and every
rule here holds for them too, with the monsters an enfeeble reached for
the people a buff reached.

- **Casts, hits and misses, and nothing else.** A buff has no amount:
  there is no total, share, average or rate to give of one, and it is in
  none of Healing's. Healing's Casts and Avg / cast are of heals alone.
- **A cast is one use**, however many it reached: a Protectra on six is one.
  One that took effect and one that had none are both casts.
- **Hit and Miss count the people reached**, not casts: each person a cast
  reached is a hit if it took effect on them and a miss if it had none
  (the addon's `hit`). A Protectra on six that took on four is one cast,
  four hits and two misses. Counting casts instead would make that
  Protectra one hit and hide the two it was wasted on. Hit % is hits out of
  hits and misses; a character's line adds up their actions'. There is no
  low mark on it: an Erase that found nothing to remove is a miss and
  often meant to be.
- **An enfeeble's miss is every monster it did not take on**: one that
  resisted, and one it had no effect on (already asleep, immune). A Sleepga
  on a sleeping monster is a cast spent for nothing, as an Erase that found
  nothing is; one column for both keeps the two views one table.
- **Per person reached**, under an action opened: the same three for each
  person (or pet) it reached. Casts there are the casts that reached them,
  each once however many results it had on them, so a person's Casts can
  be fewer than the action's but never more. Most hits first.
- **A monster reached has no colour and no alias**: only the party's names
  take colour slots and aliases, and asking for a monster's would spend one.
- **Only what the party did, and never a debuff on the party.** A monster's
  Slow on a character is not in the file (the addon writes only actions one
  of ours took), and a support row needs both ends ours. On a target of
  ours the addon writes only actions on its support list, by action id:
  spells and abilities the game lets nobody aim at an enemy. A message
  can't be trusted for this (the server answers Slow with the same message
  as Haste). So a charmed member's Lullaby or attack on the party is never
  written.
- **The session's window and the healers isolated, as heals have them**:
  from the zero to the end, a pause's casts left out, with healers isolated
  the others' too, a
  pet's credited to its owner under the pet's name (`Titan: Earthen Ward`).
- **It starts and ends no clock.** The zero and the end are damage rows'.
- **Exported among the events, as the file has them**, and filed apart again
  when read. Enfeebles go among the events as enfeeble lines, as the addon
  wrote them.
- Compare does not list support.

## Export, import and Compare

- **Export only while paused** (`ParseFile.Export`): a running clock would be
  out of date before the file was written.
- **The file is the addon's own records, replayed.** Rows and job lines are
  stored in the wire format (`t` in milliseconds) and read back through
  `EventReader.Feed`, the same door a live line uses. With them go what the records cannot state:
  the session and the spawn kinds.
- **Death lines go among the events**, as the addon wrote them, and rows
  keep their `targetId`. A kill goes with its killing blow (see "Kills and
  deaths").
- **Every export has an event list and a heal list**, each possibly empty: a
  parse in which nobody healed has `"heals": []` and opens like any other.
  A file without the heal list is refused.
- **Only rows the session covers are written.** Filters are not: what is
  isolated, the skillchain switch and Hide names are the viewer's.
- **An import is locked** (no Start, no Pause) and takes nothing from the live
  session, which goes on being read and measured underneath.
- **An import lives in the View section** and is counted only while that
  section is on screen. Damage and Healing are always the session; leaving
  View keeps the parse open for the way back.
- **A file that is not a parse this Zerg reads is refused with one message**,
  whatever the reason (`ParseImportException.Invalid`): "Unable to load
  parse: invalid format, check VibeXI version." The reason
  (`ImportError`) goes to the log. Any reason means the same thing to the
  player, and the likeliest is a file from another version.
- **A parse of format version 1 is refused** (`ImportError.TooOld`): its
  records' times are seconds, which read as milliseconds would be January
  1970. Version 2 is the first with milliseconds; an older Zerg refuses it
  as too new.
- **A parse must have every part a version 2 export writes**: the session's
  `armedAt`, `startedAt`, `pausedAt` and `spans`, and the `kinds` and `jobs`
  lists. A parse missing one was not written by this Zerg.
- **Exports are `.zerg`, never `.jsonl`** (`ParseFile.CanSaveAs`): the newest
  `*.jsonl` in the events folder is what Zerg follows. Detection is by
  content. Files written by the browser build that came before Zerg (`.json`)
  are not supported.
- **The owner travels in the file**, so colour slot 0 and the one name Hide
  names keeps are the exporter's.
- **A compared run is an import measured by the same pipeline**
  (`Compare.Measure`), so a compared figure is the figure Zerg prints when
  that file is imported. Every change is B − A, every percentage that over A.
- **By Job re-actors onto the main job before the totals**, so a job's accuracy
  is over its combined swings. An unreported job is "Unknown job" and is never
  borrowed from the other run.
- **The shorter run's line ends where the run ends.** Carried flat it would
  claim it was still being measured.
- **A run nobody healed in healed 0**, in Healing mode as anywhere: every
  export since heal lines has its heal list, so none is none.
- **An action's two distributions share one set of bins** (`Compare.Spread`;
  a heal's, `Compare.HealSpread`), from the least hit of either run to the greatest, so a column of A's stands
  beside the column of B's for the same damage. The width is the finer of the
  two runs' own Freedman–Diaconis widths (the wider would flatten the tighter
  run), and there are at most 20, since each bin holds two columns.
- **A column there is a share of that run's hits, not a count.** Two runs are
  seldom the same length; by count the longer dwarfs the other whatever their
  shapes. The counts are in the hover card. Each run's figures are
  `Counting.Distribution`'s, the ones that file's own drill-down prints.
- **A heal's distribution is of casts**, each what it healed over everyone it
  reached, pet heals under the pet's name. A run that never cast the heal
  cast it 0 times.
- **Kills per hour, Avg kill time and Deaths** are each run's own (see
  "Kills and deaths"), the change in deaths a count ("+1"); fewer seconds
  and fewer deaths are the good way. By target names each run's kills of a
  name, "Tiger (9 · 10)", with each run's average kill time. A run with
  no kill reads 0 kills; its average kill time, with nothing to average,
  is a dash.

## Replay

An addon event file read whole, cropped to a range and exported (`Replay`,
`Tracker.Replaying`).

- **Every line goes through `EventReader.Feed`**, as a polled line does,
  into one reader. A replay is not a second way of reading the file.
- **A replay is an import**: it lives in a View tab, is read only (no Start,
  no Pause, no new lines) and takes nothing from the live session. The one
  difference is its clock, which is the range picked over it.
- **A range is measured as Start and Pause would have measured it**
  (`Replay.Crop`): armed at its start; its zero the first party row from
  there; paused at its end, and snapped back to the last party row at or
  before it (`Session.Snap`). A row at either end is in the range. The
  handles stand on whole seconds (`ReplayRange.Set`), the step they move
  by. Dragging a handle into a quiet stretch moves no clock.
- **Asked without the viewer's switches.** The zero, the end and the bars
  are of every party row, skillchains included and whoever is
  isolated, as a paused session's end is. The range is picked over rows
  already read; a zero that moved with each filter would re-date the parse,
  and an export carries no filters to work it out from again.
- **One range, one clock.** From its zero to its end, the quiet stretches
  inside it included, as a session left running through them would have it.
  The file is not cut into fights for the player: where one begins and ends
  is the player's to say, with the handles. Its kills and deaths are ticks
  under the bars, at their death lines' times, to say it by.
- **A file with no party damage cannot be replayed**: nothing to measure, so
  it is refused, not opened empty. A range with no party row in
  it counts nothing and cannot be exported.
- **The file is read once.** The live file can be replayed too: what it held
  when it was read, up to its last whole line. Closing the tab and opening
  it again reads it again.
- **An export of a range is an ordinary parse**, made by `Tracker.Export`
  like any other: only the rows the range covers, the roster, and the range
  as a paused session.

## Two actions side by side

- **A pair is two sides, each a character and an action** (in Compare,
  of its run). The same action on both sides is what the tables already
  had; a pair lets the actions differ (Tachi: Shoha against Tachi: Jinpu)
  and the characters too (one SAM's Tachi: Fudo against another's). A is
  the baseline as everywhere: every change is B − A, every percentage that
  over A.
- **Two actions share one set of bins**, by the rule two runs' one action
  does (`Compare.Shared`): the least hit of either to the greatest, the
  finer of the two widths, a column a share of its own side's hits. The
  live drill-down and Compare go through the same code, so a pair binned
  in one is binned the same in the other.
- **The live drill-down's pair is of what the drill-down already counts**:
  each side is `Counting.Distribution` over the session's rows (a heal's,
  over its casts), the figures each side's own drill-down would print.
  Damage leads with Hits and Accuracy, as the single drill-down's figures
  end with accuracy; healing leads with Casts. Every hit in the list is
  measured against its own side's average.
- **A pair goes where its sides go.** It closes with its drill-down; a
  skillchain on either side goes when the chains are switched off (on A,
  the drill-down closes; on B, B goes); with targets or types isolated, a
  B never used on them goes and A stays. Kept per parse with the
  drill-down, never saved.
- **In Compare a pair the player made is a row of its own**, under A's
  row, before that row's own actions, and nothing is hidden for it: the
  same-name rows stay. A's side is always the row's (a pair with another
  character's action as A is made under that character); B's can be any
  row's. Swap takes each pair with its runs (B's action is A's now, under
  the other row). A new file in a slot, or Character | Job, drops them
  with the opened rows.

## Colours and names

- **A character is drawn in their job's colour.** The palette is the one
  players already read in game. It is not colourblind-separable (four jobs are
  reds), which is acceptable only because a colour never appears without the
  name and job beside it as text.
- **Two characters on one job are shaded** (`Shades.Step`), ordered by colour
  slot so a shade is stable all session.
- **A live pair's sides are in characters' colours, Compare's in the
  runs'.** In a live drill-down A is its character's colour and B the
  other character's, unless the two are too close to tell apart (one
  character against themself, or two near colours: under 48 apart in RGB),
  when B is A's next shade that is far enough (`Shades.Beside`). White has
  no lighter shade on the dark page, so a WHM's B is the darker one. The
  tag's letter says which side each is, so the colour is never the only
  mark. In Compare the sides are runs and keep the runs' colours.
- **No job on record, or a job with no colour, falls to the 18 slots**
  (`Cast`). The file's owner is pinned to slot 0 before anyone else is
  slotted; otherwise their hue depends on who swung first. Monsters get no
  slot.
- **Hide names is display only.** Every key stays the real name. If a total
  moves when it is switched, something has started keying on a drawn name.
  Aliases are ordered by slot (`SAM/WAR`, `SAM/WAR 2`), the owner keeps their
  name, a pet keeps its own, and the Job column is dropped, not blanked.

## Known gaps

- Reaction attempts that dealt nothing are not recorded, so a Counter or
  Retaliation row always reads 100% accuracy.
- Monster TP moves and pet abilities are named `#<id>`.
- A few job abilities share an id with a weaponskill (Jump among them). The
  addon writes them as `kind:"ability"` under their own names.
- Absorbed and "no effect" outcomes are misses, except in Accuracy, where a
  swing into shadows is a hit.
- Nothing reports double, triple or quad attack rates.
- MP drain and TP are not read, except as an enfeeble's (Aspir, Absorb-TP)
  took-or-not. Every row carries its `msg`, so adding them is a change to
  the addon's tables, not to the format.
