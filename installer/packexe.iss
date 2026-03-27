#define AppName "SteamWorld Heist Українізатор"
#define AppVersion "0.50"
#define AppPublisher "EMP_UA"
#define AppURL "https://t.me/EMP_UA"
#define AppId "{{A8D7E3B2-F7C1-4B9A-9D8E-5C1B3A4D5E6F}" ; Унікальний ID для реєстру Windows

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; --- Налаштування шляхів ---
; Завдяки тому, що скрипт у ver 050, ми використовуємо відносні шляхи
DefaultDirName={code:GetSteamPath}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Дозволяємо вибір папки (для дисків D:, E: тощо)
DisableDirPage=no
DirExistsWarning=no

; --- Налаштування вихідного файлу ---
OutputDir=Output
OutputBaseFilename=SteamWorldHeist_UA_v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Messages]
ukrainian.SelectDirDesc=Виберіть папку, у якій встановлено SteamWorld Heist.
ukrainian.SelectDirLabel3=Інсталятор встановить українізатор (версія {#AppVersion}) у вказану папку.

[Files]
; Оскільки скрипт лежить у ver 050, шлях веде прямо в "чисту" папку для видачі
Source: "ForDownload\SteamWorld Heist\Bundle\*"; DestDir: "{app}\Bundle"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "ForDownload\SteamWorld Heist\DLC\*"; DestDir: "{app}\DLC"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Записуємо версію в реєстр користувача (найбезпечніший метод)
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[Code]
// 1. Пошук гри в Steam
function GetSteamPath(Param: String): String;
var
  Path: String;
begin
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 322190', 'InstallLocation', Path) or
     RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Valve\Steam', 'SteamPath', Path) then
  begin
    if Pos('SteamWorld Heist', Path) > 0 then
      Result := Path
    else
      Result := Path + '\steamapps\common\SteamWorld Heist';
  end
  else
    Result := ExpandConstant('{pf32}\Steam\steamapps\common\SteamWorld Heist');
end;

// 2. Перевірка версії та наявності EXE перед початком
function InitializeSetup(): Boolean;
var
  OldVersion: String;
begin
  Result := True;
  // Перевіряємо, чи вже встановлена якась версія
  if RegQueryStringValue(HKCU, 'Software\{#AppPublisher}\{#AppName}', 'Version', OldVersion) then
  begin
    if OldVersion = '{#AppVersion}' then
    begin
      if MsgBox('Українізатор версії ' + OldVersion + ' вже встановлено. Бажаєте перевстановити його?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  // Перевірка папки на етапі її вибору
  if CurPageID = wpSelectDir then
  begin
    if not FileExists(ExpandConstant('{app}\SteamWorldHeist.exe')) then
    begin
      if MsgBox('У вказаній папці не знайдено SteamWorldHeist.exe.' #13#10 #13#10 'Ви впевнені, що хочете встановити переклад саме сюди?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;
