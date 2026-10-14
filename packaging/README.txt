Zerg {version}
===========

Zerg shows your party's damage and healing in FFXI (HorizonXI), live, while
you play: totals, DPS, every action and every hit, and floating panels you
can lay over the game.


WHAT YOU NEED
-------------

- Windows 10 or 11, 64-bit. Nothing else to install: Zerg.exe carries the
  .NET runtime it needs.

- The VibeXI addon, running in game. It is where every number comes from:
  it writes one line per event to %LOCALAPPDATA%\VibeXI\events\, and Zerg
  reads those files. Without it Zerg has nothing to show. Install it by
  following the instructions at https://github.com/HasayaFFXI/VibeXI


GETTING STARTED
---------------

1. Unzip this folder wherever you like (not inside Program Files).
2. Run Zerg.exe. Windows may say it "protected your PC", because Zerg is
   not signed: choose "More info", then "Run anyway". It asks once.
3. In game, with VibeXI loaded, press Start in Zerg before the pull. The
   clock starts on the first hit that counts; Pause when the fight is over.

Pressing Start again during a session asks first (Confirm or Cancel), so a
fight is never thrown away by a slip.

Pop out any card to float it over the game. Ctrl+Alt+Z switches the
floating panels to click-through, so the game can be clicked through them;
the key can be changed on the Settings page (the gear).

Export saves a paused parse as a .zerg file; Import opens one in a tab of
its own, and Compare sets two side by side. To open a .zerg file with a
double-click, and give such files Zerg's icon, press "Open .zerg files with
Zerg" on the Settings page. If you move Zerg.exe afterwards, press it
again. Then several selected together and opened with Enter open a tab
each.


WHERE ZERG KEEPS THINGS
-----------------------

%LOCALAPPDATA%\VibeXI\zerg\settings.json   your settings and window places
%LOCALAPPDATA%\VibeXI\zerg\logs\zerg.log   what Zerg did; send it with a bug report

To remove Zerg, close it, press "Stop" under Parse files on the Settings
page if you set that up, delete its folder, and delete
%LOCALAPPDATA%\VibeXI\zerg if you want the settings gone too.
