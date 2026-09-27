; ==============================================================================
; packexe.iss
; UA: Інсталятор української локалізації для SteamWorld Heist.
; EN: Ukrainian localization installer for SteamWorld Heist.
; Автор / Author: EMP_UA (https://github.com/EMP-UA)
; ==============================================================================

#define AppName "SteamWorld Heist Українізатор"
#define AppVersion "1.02"
#define AppPublisher "EMP_UA"
#define AppURL "https://emp-ua.com/"
#define AppId "{{A8D7E3B2-F7C1-4B9A-9D8E-5C1B3A4D5E6F}" ; UA: Унікальний ID для реєстру Windows / EN: Unique ID for the Windows registry
#define SteamAppId "322190" ; UA: Офіційний ID гри SteamWorld Heist у Steam / EN: Official Steam App ID for SteamWorld Heist

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; --- UA: Налаштування шляхів / EN: Path settings ---
DefaultDirName={code:GetSteamPath}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; UA: Дозволяємо вибір теки вручну / EN: Allow manual folder selection
DisableDirPage=no
DirExistsWarning=no

; UA: Закривати гру, якщо вона запущена, і не пропонувати попередню теку —
;     шлях завжди перераховується наново через GetSteamPath.
; EN: Close the game if it is running, and never reuse a previously chosen
;     folder — the path is always recalculated via GetSteamPath.
CloseApplications=yes
UsePreviousAppDir=no

; --- UA: Налаштування вихідного файлу / EN: Output file settings ---
OutputDir=Output
OutputBaseFilename=SteamWorldHeist_UA_v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
ukrainian.SelectDirDesc=Виберіть теку, у якій встановлено SteamWorld Heist.
ukrainian.SelectDirLabel3=Інсталятор встановить українізатор (версія {#AppVersion}) у вказану теку.
english.SelectDirDesc=Select the folder where SteamWorld Heist is installed.
english.SelectDirLabel3=The installer will place the localization (version {#AppVersion}) into the selected folder.

[CustomMessages]
ukrainian.LaunchGame=Запустити гру
english.LaunchGame=Launch the game
ukrainian.ViewReadme=Переглянути Readme
english.ViewReadme=View Readme
ukrainian.GitHubSourceLabel=🛠 Вихідний код інсталятора (GitHub)
english.GitHubSourceLabel=🛠 Installer source code (GitHub)
ukrainian.FinishedSiteLink=🌐 emp-ua.com/localizations — проєкти автора
english.FinishedSiteLink=🌐 emp-ua.com/localizations — author's projects
ukrainian.FinishedDiscussionServers= — сервери для обговорень
english.FinishedDiscussionServers= — discussion servers
ukrainian.FinishedNexusModsLink=🟠 NexusMods — слідкуйте за оновленнями локалізацій
english.FinishedNexusModsLink=🟠 NexusMods — follow localizations updates
ukrainian.ReinstallConfirm=Українізатор версії %1 вже встановлено. Бажаєте перевстановити його?
english.ReinstallConfirm=Version %1 of the localization is already installed. Would you like to reinstall it?
ukrainian.OlderVersionFound=Знайдено попередню версію: %1%nБуде встановлено версію: %2
english.OlderVersionFound=Previous version found: %1%nVersion %2 will be installed.
ukrainian.GameFilesNotFound=У вказаній папці не знайдено файлів гри (%1).%n%nВи впевнені, що хочете встановити файли сюди?
english.GameFilesNotFound=Game files not found in the selected folder (%1).%n%nAre you sure you want to install the files here?

[Files]
; UA: Шляхи до файлів перекладу / EN: Localization file paths
Source: "SteamWorld Heist 101\Bundle\*"; DestDir: "{app}\Bundle"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "SteamWorld Heist 101\DLC\*"; DestDir: "{app}\DLC"; Flags: ignoreversion recursesubdirs createallsubdirs
; UA: Текстовий Readme — необов'язковий локальний файл (не в git),
;     кладеться поруч зі скриптом перед компіляцією інсталятора.
; EN: Text readme — an optional, local-only file (not in git), placed
;     next to this script before the installer is compiled.
Source: "Readme.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Registry]
; UA: Записуємо версію в реєстр користувача / EN: Write the version to the user registry
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[Run]
; UA: Необов'язковий запуск гри після встановлення (без галочки за замовчуванням)
; EN: Optional game launch after installation (unchecked by default)
Filename: "steam://rungameid/{#SteamAppId}"; Description: "{cm:LaunchGame}"; Flags: shellexec postinstall nowait skipifsilent unchecked
; UA: Необов'язковий перегляд Readme після встановлення (без галочки за замовчуванням)
; EN: Optional readme view after installation (unchecked by default)
Filename: "{app}\Readme.txt"; Description: "{cm:ViewReadme}"; Flags: postinstall shellexec nowait skipifsilent unchecked

[Code]
var
  GitHubLabel: TNewStaticText;
  ShellExecErrorCode: Integer;
  FinishedLinksCreated: Boolean;

// UA: Клік на посилання вихідного коду інсталятора в нижньому лівому куті вікна.
// EN: Click on the installer source-code link in the wizard's bottom-left corner.
procedure GitHubLabelClick(Sender: TObject);
begin
  ShellExec('open', 'https://github.com/EMP-UA/SteamWorld-Heist-UA-Localization/blob/main/installer/packexe.iss', '', '', SW_SHOWNORMAL, ewNoWait, ShellExecErrorCode);
end;

procedure InitializeWizard();
begin
  GitHubLabel := TNewStaticText.Create(WizardForm);
  GitHubLabel.Top := WizardForm.ClientHeight - 28; // UA: лівий нижній кут / EN: bottom-left corner
  GitHubLabel.Left := 20;
  GitHubLabel.Anchors := [akLeft, akBottom];
  GitHubLabel.Caption := CustomMessage('GitHubSourceLabel');
  GitHubLabel.Font.Color := clHotLight;
  GitHubLabel.Font.Style := [fsUnderline];
  GitHubLabel.Cursor := crHand;
  GitHubLabel.OnClick := @GitHubLabelClick;
  GitHubLabel.Parent := WizardForm;
end;

{ UA: Блок посилань автора на сторінці "Завершено": сайт (розділ
  проєктів), Telegram і Discord, YouTube і Twitch, а також сторінки
  саме цієї локалізації — NexusMods, Steam Workshop і стаття на
  kuli.com.ua. Усього п'ять рядків.
  EN: A block of the author's own links on the "Finished" page: the
  website (projects section), Telegram and Discord, YouTube and
  Twitch, plus this specific localization's own pages — NexusMods,
  Steam Workshop and the kuli.com.ua article. Five lines total. }
procedure OpenAuthorLink(const Url: String);
var
  LinkErrorCode: Integer;
begin
  ShellExec('open', Url, '', '', SW_SHOWNORMAL, ewNoWait, LinkErrorCode);
end;

procedure SiteLinkClick(Sender: TObject);
begin
  OpenAuthorLink('https://emp-ua.com/localizations/');
end;

procedure TelegramLinkClick(Sender: TObject);
begin
  OpenAuthorLink('https://t.me/EMP_UA');
end;

procedure DiscordLinkClick(Sender: TObject);
begin
  OpenAuthorLink('https://discord.gg/QdmgsCgPkp');
end;

procedure YouTubeLinkClick(Sender: TObject);
begin
  OpenAuthorLink('https://www.youtube.com/@EMPs_UA');
end;

procedure TwitchLinkClick(Sender: TObject);
begin
  OpenAuthorLink('https://www.twitch.tv/emp_ua');
end;

procedure NexusModsLinkClick(Sender: TObject);
begin
  OpenAuthorLink('https://www.nexusmods.com/profile/EMPsUA/mods');
end;

{ UA: Клікабельний фрагмент рядка (посилання) — підкреслений, кольору
  посилання. Left передається явно, щоб кілька посилань могли стояти
  в одному рядку одне за одним.
  EN: A clickable line fragment (link) — underlined, link-colored. Left
  is passed explicitly so several links can sit on the same line, one
  after another. }
function MakeFinishedLink(Top, Left: Integer; const Caption: String): TNewStaticText;
begin
  Result := TNewStaticText.Create(WizardForm);
  Result.Parent := WizardForm.FinishedPage;
  Result.AutoSize := True;
  Result.Left := Left;
  Result.Top := Top;
  Result.Anchors := [akLeft, akBottom];
  Result.Caption := Caption;
  Result.Font.Color := clHotLight;
  Result.Font.Style := [fsUnderline];
  Result.Cursor := crHand;
end;

{ UA: Неклікабельний фрагмент того самого рядка — роздільник між двома
  посиланнями або опис після них.
  EN: A non-clickable fragment of the same line — a separator between
  two links, or the description that follows them. }
function MakeFinishedText(Top, Left: Integer; const Caption: String): TNewStaticText;
begin
  Result := TNewStaticText.Create(WizardForm);
  Result.Parent := WizardForm.FinishedPage;
  Result.AutoSize := True;
  Result.Left := Left;
  Result.Top := Top;
  Result.Anchors := [akLeft, akBottom];
  Result.Caption := Caption;
end;

procedure CreateFinishedPageLinks();
var
  BaseTop, LineLeft: Integer;
  SiteLink, TelegramLink, DiscordLink, YouTubeLink, TwitchLink, NexusModsLink: TNewStaticText;
  Sep1, Desc1, Sep2, Desc2: TNewStaticText;
begin
  if FinishedLinksCreated then
    Exit;
  FinishedLinksCreated := True;

  LineLeft := WizardForm.FinishedLabel.Left;
  BaseTop := WizardForm.FinishedPage.ClientHeight - 98;

  { UA: Рядок 1 — сайт (сторінка проєктів автора).
    EN: Line 1 — website (the author's projects page). }
  SiteLink := MakeFinishedLink(BaseTop, LineLeft, CustomMessage('FinishedSiteLink'));
  SiteLink.OnClick := @SiteLinkClick;

  { UA: Рядок 2 — Telegram і Discord (сервери для обговорень).
    EN: Line 2 — Telegram and Discord (discussion servers). }
  TelegramLink := MakeFinishedLink(BaseTop + 22, LineLeft, '🔵 Telegram');
  TelegramLink.OnClick := @TelegramLinkClick;
  Sep1 := MakeFinishedText(BaseTop + 22, TelegramLink.Left + TelegramLink.Width, ' · ');
  DiscordLink := MakeFinishedLink(BaseTop + 22, Sep1.Left + Sep1.Width, '🟣 Discord');
  DiscordLink.OnClick := @DiscordLinkClick;
  Desc1 := MakeFinishedText(BaseTop + 22, DiscordLink.Left + DiscordLink.Width, CustomMessage('FinishedDiscussionServers'));

  { UA: Рядок 3 — YouTube і Twitch (українізований ігролад).
    EN: Line 3 — YouTube and Twitch (Ukrainianized gameplay). }
  YouTubeLink := MakeFinishedLink(BaseTop + 44, LineLeft, '🔴 YouTube');
  YouTubeLink.OnClick := @YouTubeLinkClick;
  Sep2 := MakeFinishedText(BaseTop + 44, YouTubeLink.Left + YouTubeLink.Width, ' · ');
  TwitchLink := MakeFinishedLink(BaseTop + 44, Sep2.Left + Sep2.Width, '🟢 Twitch');
  TwitchLink.OnClick := @TwitchLinkClick;
  Desc2 := MakeFinishedText(BaseTop + 44, TwitchLink.Left + TwitchLink.Width, CustomMessage('FinishedUkrainianGameplay'));

  { UA: Рядок 4 — сторінка локалізацій на NexusMods.
    EN: Line 4 — the NexusMods localization page. }
  NexusModsLink := MakeFinishedLink(BaseTop + 66, LineLeft, CustomMessage('FinishedNexusModsLink'));
  NexusModsLink.OnClick := @NexusModsLinkClick;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    CreateFinishedPageLinks();
end;

{ UA: Перевіряє, чи шлях НЕ містить заборонених у Windows символів
  (окрім двокрапки одразу після літери диска, напр. "C:").
  EN: Checks that the path does NOT contain characters forbidden on
  Windows (except the colon right after a drive letter, e.g. "C:"). }
function IsValidWindowsPath(const Path: String): Boolean;
var
  CheckPath: String;
begin
  Result := False;
  if Path = '' then
    Exit;

  CheckPath := Path;
  if (Length(CheckPath) >= 2) and (CheckPath[2] = ':') then
    Delete(CheckPath, 1, 2);

  if (Pos('/', CheckPath) > 0) or (Pos(':', CheckPath) > 0) or
     (Pos('*', CheckPath) > 0) or (Pos('?', CheckPath) > 0) or
     (Pos('"', CheckPath) > 0) or (Pos('<', CheckPath) > 0) or
     (Pos('>', CheckPath) > 0) or (Pos('|', CheckPath) > 0) then
  begin
    Result := False;
    Exit;
  end;

  Result := True;
end;

// UA: 1. Пошук гри в Steam / EN: 1. Locate the game via Steam
function GetSteamPath(Param: String): String;
var
  Path: String;
  DefaultResult: String;
begin
  DefaultResult := ExpandConstant('{pf32}\Steam\steamapps\common\SteamWorld Heist');
  Result := DefaultResult;

  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {#SteamAppId}', 'InstallLocation', Path) then
  begin
    Log('GetSteamPath: сире значення InstallLocation = "' + Path + '"');
    Path := Trim(Path);
    StringChange(Path, '"', '');
    StringChange(Path, '/', '\');
    if (Path <> '') and (Path[Length(Path)] = '\') then
      Path := Copy(Path, 1, Length(Path) - 1);

    // UA: Деякі записи Uninstall містять корінь Steam, а не саму теку гри —
    //     тоді дописуємо стандартний підшлях common\SteamWorld Heist.
    // EN: Some Uninstall entries hold the Steam root rather than the game
    //     folder itself — append the standard common\SteamWorld Heist path.
    if (Path <> '') and (Pos('SteamWorld Heist', Path) = 0) then
      Path := Path + '\steamapps\common\SteamWorld Heist';

    if IsValidWindowsPath(Path) then
    begin
      Log('GetSteamPath: використано шлях з Uninstall-ключа гри: ' + Path);
      Result := Path;
      Exit;
    end
    else
      Log('GetSteamPath: шлях з Uninstall-ключа НЕВАЛІДНИЙ, пропускаємо: ' + Path);
  end
  else
    Log('GetSteamPath: ключ Uninstall\Steam App {#SteamAppId} не знайдено');

  if RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Valve\Steam', 'SteamPath', Path) then
  begin
    Log('GetSteamPath: сире значення SteamPath = "' + Path + '"');
    Path := Trim(Path);
    StringChange(Path, '"', '');
    StringChange(Path, '/', '\');

    if Path <> '' then
    begin
      Path := AddBackslash(Path) + 'steamapps\common\SteamWorld Heist';
      if IsValidWindowsPath(Path) then
      begin
        Log('GetSteamPath: використано шлях, побудований з SteamPath: ' + Path);
        Result := Path;
        Exit;
      end
      else
        Log('GetSteamPath: побудований шлях НЕВАЛІДНИЙ, пропускаємо: ' + Path);
    end;
  end
  else
    Log('GetSteamPath: ключ HKCU\Software\Valve\Steam не знайдено');

  Log('GetSteamPath: використано шлях за замовчуванням: ' + DefaultResult);
  Result := DefaultResult;
end;

// UA: 2. Перевірка версії перед початком встановлення
// EN: 2. Version check before the installation starts
function InitializeSetup(): Boolean;
var
  OldVersion: String;
begin
  Result := True;
  if RegQueryStringValue(HKCU, 'Software\{#AppPublisher}\{#AppName}', 'Version', OldVersion) then
  begin
    if OldVersion = '{#AppVersion}' then
    begin
      if MsgBox(FmtMessage(CustomMessage('ReinstallConfirm'), [OldVersion]), mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end
    else
      MsgBox(FmtMessage(CustomMessage('OlderVersionFound'), [OldVersion, '{#AppVersion}']), mbInformation, MB_OK);
  end;
end;

// UA: 3. Перевірка правильності теки / EN: 3. Folder sanity check
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if not FileExists(ExpandConstant('{app}\SteamWorldHeist.exe')) then
    begin
      if MsgBox(FmtMessage(CustomMessage('GameFilesNotFound'), ['SteamWorldHeist.exe']), mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;

// UA: 4. Логіка видалення (лише текстове попередження, двомовне)
// EN: 4. Uninstall logic (text warning only, bilingual)
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if ActiveLanguage = 'ukrainian' then
      MsgBox('Українізатор успішно видалено.' #13#10#13#10 +
             'УВАГА: Оскільки переклад замінював оригінальні файли гри, зараз у грі відсутні деякі важливі файли (гра не запуститься).' #13#10#13#10 +
             'Щоб відновити оригінальну англійську версію, виконайте наступні дії:' #13#10 +
             '1. Відкрийте Steam' #13#10 +
             '2. Натисніть правою кнопкою миші на SteamWorld Heist' #13#10 +
             '3. Оберіть "Властивості" -> "Встановлені файли"' #13#10 +
             '4. Натисніть "Перевірити цілісність файлів гри"',
             mbInformation, MB_OK)
    else
      MsgBox('The localization has been successfully removed.' #13#10#13#10 +
             'WARNING: Since the translation replaced the original game files, some important files are now missing (the game will not start).' #13#10#13#10 +
             'To restore the original English version:' #13#10 +
             '1. Open Steam' #13#10 +
             '2. Right-click on SteamWorld Heist' #13#10 +
             '3. Select "Properties" -> "Installed Files"' #13#10 +
             '4. Click "Verify integrity of game files"',
             mbInformation, MB_OK);
  end;
end;
