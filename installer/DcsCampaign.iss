; Warclock Installer (Inno Setup 6). Bauen: installer\build.cmd -> dist\Warclock-Setup.exe
; Keine Adminrechte, keine Änderung an DCS. Spielstand (%LOCALAPPDATA%\DCS-Kampagne) bleibt beim Deinstallieren.
; Interne Namen (Ordner, DcsCampaign.exe, AppId) bleiben – Spielstände, Updates und DCS-ATC hängen daran.
#define Src ".."
#define AppVer GetStringFileInfo(Src + "\tmp\sc\DcsCampaign.exe", "ProductVersion")

[Setup]
AppId={{8F3A9C52-1E7B-4B6D-A2C4-DC5A7C0A7C02}
AppName=Warclock
AppVersion={#AppVer}
AppVerName=Warclock {#AppVer}
AppPublisher=Iceman1991
VersionInfoVersion={#AppVer}
DefaultDirName={localappdata}\Programs\DCS-Kampagne
DefaultGroupName=Warclock
UsePreviousGroup=no
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir={#Src}\dist
OutputBaseFilename=Warclock-Setup
SetupIconFile={#Src}\src\DcsCampaign\app.ico
UninstallDisplayIcon={app}\DcsCampaign.exe
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayName=Warclock
LanguageDetectionMethod=none
ShowLanguageDialog=no
CloseApplications=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "hook"; Description: "Live connection to DCS: hook in Saved Games\DCS\Scripts\Hooks (new flights and convoys during the mission; multiplayer: slot locking, the server loads the next mission itself)"; Flags: unchecked
Name: "splash"; Description: "Splash Damage script (Stevey666, recommended, download separately – see README): build it into every campaign mission (realistic blast radius, cook-offs)"
Name: "atc"; Description: "DCS-ATC (tower, AWACS, radio – separate project): downloaded (about 790 MB) and installed on the first start"

[Files]
Source: "{#Src}\tmp\sc\DcsCampaign.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Src}\tmp\sc\web\*"; DestDir: "{app}\web"; Flags: ignoreversion recursesubdirs
Source: "{#Src}\tmp\sc\mod\*"; DestDir: "{app}\mod"; Flags: ignoreversion recursesubdirs
Source: "{#Src}\tmp\sc\mod\DcsCampaignHook.lua"; DestDir: "{code:SavedGames}\Scripts\Hooks"; Tasks: hook; Flags: ignoreversion
Source: "{#Src}\tmp\sc\scripts\Splash_Damage*.lua"; DestDir: "{localappdata}\DCS-Kampagne\scripts"; Tasks: splash; Flags: ignoreversion skipifsourcedoesntexist uninsneveruninstall
Source: "{#Src}\installer\README.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "{#Src}\installer\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Src}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[INI]
; Wahl der optionalen Teile – die App liest sie ({app}\options.ini)
Filename: "{app}\options.ini"; Section: "Options"; Key: "Splash"; String: "1"; Tasks: splash; Flags: uninsdeletesection
Filename: "{app}\options.ini"; Section: "Options"; Key: "Splash"; String: "0"; Tasks: not splash; Flags: uninsdeletesection
Filename: "{app}\options.ini"; Section: "Options"; Key: "Atc"; String: "1"; Tasks: atc
Filename: "{app}\options.ini"; Section: "Options"; Key: "Atc"; String: "0"; Tasks: not atc

[Icons]
Name: "{group}\Warclock"; Filename: "{app}\DcsCampaign.exe"; WorkingDir: "{app}"; Flags: runminimized
Name: "{group}\Campaign missions (folder)"; Filename: "{code:SavedGames}\Missions\Kampagne"
Name: "{group}\Readme"; Filename: "{app}\README.txt"
Name: "{group}\Uninstall Warclock"; Filename: "{uninstallexe}"

[InstallDelete]
; Startmenü-Ordner vor dem Namen Warclock
Type: filesandordirs; Name: "{userprograms}\DCS-Kampagne"
; deutsche Einträge älterer Versionen
Type: files; Name: "{app}\LIESMICH.txt"
Type: files; Name: "{group}\Kampagnen-Missionen (Ordner).lnk"
Type: files; Name: "{group}\Liesmich.lnk"
Type: files; Name: "{group}\DCS-Kampagne deinstallieren.lnk"

[Dirs]
Name: "{code:SavedGames}\Missions\Kampagne"; Flags: uninsneveruninstall

[Run]
Filename: "{app}\DcsCampaign.exe"; WorkingDir: "{app}"; Description: "Start Warclock now"; Flags: postinstall nowait skipifsilent runminimized

[Code]
var Saved: String;

{ Saved Games\DCS (bzw. DCS.openbeta, falls nur das existiert) – wie DcsCampaign.exe }
function SavedGames(Param: String): String;
var Base: String;
begin
  if Saved = '' then begin
    Base := GetEnv('USERPROFILE') + '\Saved Games';
    if not DirExists(Base + '\DCS') and DirExists(Base + '\DCS.openbeta') then Saved := Base + '\DCS.openbeta'
    else Saved := Base + '\DCS';
  end;
  Result := Saved;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not DirExists(SavedGames('')) then
    Result := MsgBox('DCS World was not found (Saved Games\DCS is missing). Install anyway?', mbConfirmation, MB_YESNO) = IDYES;
end;
