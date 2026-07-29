#define AppName "SteamWorld Heist Українізатор"
#define AppVersion "1.01"
#define AppPublisher "EMP_UA"
#define AppURL "https://t.me/EMP_UA"
#define AppId "{{A8D7E3B2-F7C1-4B9A-9D8E-5C1B3A4D5E6F}" ; Унікальний ID для реєстру Windows
#define SteamAppId "322190" ; Офіційний ID гри SteamWorld Heist у Steam

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; --- Налаштування шляхів ---
DefaultDirName={code:GetSteamPath}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Дозволяємо вибір папки
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
; Шляхи до файлів перекладу
Source: "SteamWorld Heist 101\Bundle\*"; DestDir: "{app}\Bundle"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "SteamWorld Heist 101\DLC\*"; DestDir: "{app}\DLC"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Записуємо версію в реєстр користувача
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[Code]
// 1. Пошук гри в Steam
function GetSteamPath(Param: String): String;
var
  Path: String;
begin
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {#SteamAppId}', 'InstallLocation', Path) or
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
  if RegQueryStringValue(HKCU, 'Software\{#AppPublisher}\{#AppName}', 'Version', OldVersion) then
  begin
    if OldVersion = '{#AppVersion}' then
    begin
      if MsgBox('Українізатор версії ' + OldVersion + ' вже встановлено. Бажаєте перевстановити його?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;

// 3. Перевірка правильності папки
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if not FileExists(ExpandConstant('{app}\SteamWorldHeist.exe')) then
    begin
      if MsgBox('У вказаній папці не знайдено SteamWorldHeist.exe.' #13#10 #13#10 'Ви впевнені, що хочете встановити переклад саме сюди?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;

// 4. ЛОГІКА ВИДАЛЕННЯ (Тільки текстове попередження)
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Виконується після того, як файли перекладу були видалені
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBox('Українізатор успішно видалено.' #13#10#13#10 +
           'УВАГА: Оскільки переклад замінював оригінальні файли гри, зараз у грі відсутні деякі важливі файли (гра не запуститься).' #13#10#13#10 +
           'Щоб відновити оригінальну англійську версію, виконайте наступні дії:' #13#10 +
           '1. Відкрийте Steam' #13#10 +
           '2. Натисніть правою кнопкою миші на SteamWorld Heist' #13#10 +
           '3. Оберіть "Властивості" -> "Встановлені файли"' #13#10 +
           '4. Натисніть "Перевірити цілісність файлів гри"', 
           mbInformation, MB_OK);
  end;
end;
