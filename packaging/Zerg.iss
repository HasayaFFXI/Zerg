; Zerg's installer: Zerg-<version>-setup.exe, built by tools/package.py with
; Inno Setup 6 (winget install JRSoftware.InnoSetup), which passes:
;
;   /DAppVersion=<version>   the <Version> in Directory.Build.props
;   /DSourceDir=<folder>     the folder holding the published Zerg.exe, README.txt
;                            and LICENSE.txt
;   /O<folder>               where the setup exe is written
;
; Installed for the signed-in user alone, so it asks for no administrator:
; Zerg's settings (%LOCALAPPDATA%\VibeXI\zerg) and its .zerg association
; (HKCU\Software\Classes) are the user's already. The association written
; here is the one the Settings page writes (src/Zerg/Native/FileAssociation.cs);
; the two must stay the same keys.
;
; Kept ASCII.

#ifndef AppVersion
  #error Pass /DAppVersion=<version> (tools/package.py does)
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=<folder holding Zerg.exe> (tools/package.py does)
#endif

#define VibeXI "https://github.com/HasayaFFXI/VibeXI"

[Setup]
; Never change the AppId: it is how a newer setup finds the Zerg it upgrades.
AppId={{4DDE8769-7FF5-44AE-B733-E79FFA0FBB21}
AppName=Zerg
AppVersion={#AppVersion}
AppVerName=Zerg {#AppVersion}
AppPublisher=HasayaFFXI
AppPublisherURL=https://github.com/HasayaFFXI/Zerg
AppSupportURL=https://github.com/HasayaFFXI/Zerg
VersionInfoVersion={#AppVersion}
VersionInfoProductName=Zerg

PrivilegesRequired=lowest
; {autopf} is %LOCALAPPDATA%\Programs for a per-user install.
DefaultDirName={autopf}\Zerg
DisableProgramGroupPage=yes
DefaultGroupName=Zerg
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Setup shows the license on a page of its own, to read, not to accept: the
; GPL asks no one to accept it to receive or run Zerg (its section 9).
InfoBeforeFile={#SourceDir}\LICENSE.txt

; A running Zerg holds this mutex (Native/SingleInstance): setup and the
; uninstaller ask for it to be closed first.
AppMutex=Local\VibeXI.Zerg
ChangesAssociations=yes

WizardStyle=modern dynamic
SetupIconFile=..\src\Zerg\zerg.ico
; Zerg's icon tile, one file per display scale (tools/make-wizard-images.ps1),
; the same in the light and dark modes: without the DynamicDark pair a dark
; Windows gets Inno's own pictures.
#define LargeImages "wizard\large-202.png,wizard\large-269.png,wizard\large-336.png,wizard\large-403.png,wizard\large-430.png,wizard\large-498.png,wizard\large-534.png"
#define SmallImages "wizard\small-58.png,wizard\small-77.png,wizard\small-97.png,wizard\small-116.png,wizard\small-124.png,wizard\small-143.png,wizard\small-159.png"
WizardImageFile={#LargeImages}
WizardImageFileDynamicDark={#LargeImages}
WizardSmallImageFile={#SmallImages}
WizardSmallImageFileDynamicDark={#SmallImages}
UninstallDisplayName=Zerg
UninstallDisplayIcon={app}\Zerg.exe

OutputBaseFilename=Zerg-{#AppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes

[Messages]
; The license page (InfoBeforeFile), headed "Information" unless named.
WizardInfoBefore=License
InfoBeforeLabel=Zerg is free software under the GNU General Public License, version 3.
FinishedLabel=Setup has finished installing Zerg on your computer.%n%nZerg needs the VibeXI addon, running in FFXI. It is where every number comes from: without it Zerg has nothing to show. Install it by following the instructions at:

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: associate; Description: "Open .zerg parse files with Zerg"; GroupDescription: "Parse files:"

[Files]
Source: "{#SourceDir}\Zerg.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\README.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Zerg"; Filename: "{app}\Zerg.exe"
Name: "{autodesktop}\Zerg"; Filename: "{app}\Zerg.exe"; Tasks: desktopicon

[Registry]
; FileAssociation.Register's keys. Taken back by Unassociate below, not by
; uninstall flags: only while they still name this Zerg.
Root: HKCU; Subkey: "Software\Classes\.zerg"; ValueType: string; ValueName: ""; ValueData: "Zerg.Parse"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.zerg\OpenWithProgids"; ValueType: string; ValueName: "Zerg.Parse"; ValueData: ""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Zerg.Parse"; ValueType: string; ValueName: ""; ValueData: "Zerg parse"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Zerg.Parse\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Zerg.exe"",0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Zerg.Parse\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Zerg.exe"" ""%1"""; Tasks: associate

[Run]
; The order matters: CurPageChanged ticks the second by its index.
Filename: "{app}\Zerg.exe"; Description: "{cm:LaunchProgram,Zerg}"; Flags: nowait postinstall skipifsilent
Filename: "{#VibeXI}"; Description: "Open the VibeXI addon's page"; Flags: shellexec nowait postinstall skipifsilent unchecked

[Code]
const
  Classes = 'Software\Classes\';
  ProgId = 'Zerg.Parse';

var
  VibeXILink: TNewStaticText;
  RemoveSettings: Boolean;

{ The addon writes here once it has run in game: a sign it is installed. }
function VibeXIHasRun: Boolean;
begin
  Result := DirExists(ExpandConstant('{localappdata}\VibeXI\events'));
end;

procedure OpenVibeXI(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', '{#VibeXI}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

{ A text label made to look like a link: a TNewLinkLabel draws its link in a
  dark blue that the dark style cannot change, hard to read there. It takes no
  keyboard; the Run list's VibeXI box is the keyboard's way to the page. }
procedure InitializeWizard;
begin
  VibeXILink := TNewStaticText.Create(WizardForm);
  VibeXILink.Parent := WizardForm.FinishedPage;
  VibeXILink.Caption := '{#VibeXI}';
  VibeXILink.StyleElements := [seClient, seBorder];
  if IsDarkInstallMode then
    VibeXILink.Font.Color := $FFC27A
  else
    VibeXILink.Font.Color := $CC6600;
  VibeXILink.Font.Style := [fsUnderline];
  VibeXILink.Cursor := crHand;
  VibeXILink.OnClick := @OpenVibeXI;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Below: Integer;
begin
  if CurPageID <> wpFinished then Exit;
  { Setup adds "Click Finish to exit Setup." to the text; without it the link
    follows the words that lead to it. }
  WizardForm.FinishedLabel.Caption := SetupMessage(msgFinishedLabel);
  WizardForm.FinishedLabel.AdjustHeight;
  { Under the finished text, with the Run checkboxes moved down to make room. }
  VibeXILink.Left := WizardForm.FinishedLabel.Left;
  VibeXILink.Top := WizardForm.FinishedLabel.Top + WizardForm.FinishedLabel.Height + ScaleY(4);
  Below := VibeXILink.Top + VibeXILink.Height + ScaleY(20);
  if WizardForm.RunList.Top < Below then
    WizardForm.RunList.Top := Below;
  if WizardForm.RunList.Items.Count > 1 then
    WizardForm.RunList.Checked[1] := not VibeXIHasRun;
end;

{ The exe in a command line: quoted, or up to the first space (as
  FileAssociation.ExeOf). }
function ExeOf(Line: String): String;
var
  P: Integer;
begin
  Line := Trim(Line);
  if Copy(Line, 1, 1) = '"' then
  begin
    Delete(Line, 1, 1);
    P := Pos('"', Line);
    if P > 0 then
      Line := Copy(Line, 1, P - 1);
    Result := Line;
  end
  else
  begin
    P := Pos(' ', Line);
    if P > 0 then
      Result := Copy(Line, 1, P - 1)
    else
      Result := Line;
  end;
end;

{ Takes back the .zerg association while it opens this Zerg, whether setup
  or the Settings page wrote it; one that names another Zerg, or another
  program, is left alone. }
procedure Unassociate;
var
  Command, Default: String;
begin
  if not RegQueryStringValue(HKCU, Classes + ProgId + '\shell\open\command', '', Command) then
    Exit;
  if CompareText(ExeOf(Command), ExpandConstant('{app}\Zerg.exe')) <> 0 then
    Exit;
  RegDeleteKeyIncludingSubkeys(HKCU, Classes + ProgId);
  RegDeleteValue(HKCU, Classes + '.zerg\OpenWithProgids', ProgId);
  if RegQueryStringValue(HKCU, Classes + '.zerg', '', Default) and (Default = ProgId) then
    RegDeleteValue(HKCU, Classes + '.zerg', '');
  RegDeleteKeyIfEmpty(HKCU, Classes + '.zerg\OpenWithProgids');
  RegDeleteKeyIfEmpty(HKCU, Classes + '.zerg');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
      begin
        { Kept unless the player says otherwise; a silent uninstall keeps them. }
        RemoveSettings := SuppressibleMsgBox(
          'Also remove your Zerg settings and logs?' + #13#10 + #13#10 +
          ExpandConstant('{localappdata}\VibeXI\zerg') + #13#10 + #13#10 +
          'Keep them if you will install Zerg again. The VibeXI addon''s own files are not touched.',
          mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES;
        Unassociate;
      end;
    usPostUninstall:
      if RemoveSettings then
        DelTree(ExpandConstant('{localappdata}\VibeXI\zerg'), True, True, True);
  end;
end;
