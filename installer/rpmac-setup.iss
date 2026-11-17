; RPMac installer (Inno Setup 6.5.0 or newer; 6.7.x or 7.x recommended).
;
; Build: run build.bat first, then from the repo root:
;   "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\rpmac-setup.iss
;
; PawnIO (needed only on T2 Macs) is bundled by default. Put the official, unmodified
; PawnIO_setup.exe 2.2.0 in dist\ first (the compiler checks its SHA-256):
;   gh release download 2.2.0 --repo namazso/PawnIO.Setup --pattern PawnIO_setup.exe --dir dist
; To download it at install time instead (smaller setup, needs internet on T2 Macs):
;   ISCC /DPawnIODownload installer\rpmac-setup.iss

#define MyAppName "RPMac"
#define MyAppVersion "1.11.0"
#define MyAppPublisher "Javier Cabrera (golirt1)"
#define MyAppURL "https://www.github.com/golirt1/RPMAC"
#define MyAppExeName "RPMac.exe"

#if VER < EncodeVer(6,5,0)
  #error This script needs Inno Setup 6.5.0 or newer (it uses the Hash parameter in [Files]).
#endif

#ifndef RepoDir
  #define RepoDir AddBackslash(SourcePath) + ".."
#endif
#define BuildDir RepoDir + "\build"
#define DistDir RepoDir + "\dist"

#if !FileExists(BuildDir + "\" + MyAppExeName)
  #error build\RPMac.exe not found. Run build.bat first.
#endif

; ---- PawnIO, the kernel driver RPMac uses on T2 Macs (https://pawnio.eu, by namazso) ----
; Its setup prints: "Usage: PawnIOSetup.exe [-install] [-uninstall] [-unrestricted] [-debuginfo] [-silent]"
; and "This installer can be redistributed unmodified." Pin the version and its SHA-256.
#define PawnIOVersion "2.2.0"
#define PawnIOUrl "https://github.com/namazso/PawnIO.Setup/releases/download/" + PawnIOVersion + "/PawnIO_setup.exe"
#define PawnIOSha256 "1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032"

#ifndef PawnIODownload
  #ifndef PawnIOSetupSrc
    #define PawnIOSetupSrc DistDir + "\PawnIO_setup.exe"
  #endif
  #if !FileExists(PawnIOSetupSrc)
    #error dist\PawnIO_setup.exe not found. Download PawnIO 2.2.0 there, or compile with /DPawnIODownload.
  #endif
  #if GetSHA256OfFile(PawnIOSetupSrc) != PawnIOSha256
    #error PawnIO_setup.exe is not the official PawnIO 2.2.0 installer (SHA-256 mismatch).
  #endif
#endif

; RPMac's own "Install PawnIO" button (PawnIoSetup in App.cs) runs the copy next to RPMac.exe, so
; ship it there on T2 Macs too: then the button still works if the PawnIO task was unticked or failed.
#define ShipPawnIOSetupInApp

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
AppId={{686F416C-1053-4E02-95F6-D3ADF5C63DBA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Always under Program Files: RPMac's logon task runs this exe elevated, and a folder like C:\RPMac
; inherits "Authenticated Users: Modify" from C:\, so anyone could swap the exe or its DLL there.
DisableDirPage=yes
; Admin only: RPMac.exe is requireAdministrator, PawnIO is a kernel driver and the PATH entry is
; machine-wide. No PrivilegesRequiredOverridesAllowed: a per-user install can't do any of that.
PrivilegesRequired=admin
; Tell Explorer to reload the environment after the PATH change (and again after uninstall).
ChangesEnvironment=yes
; RPMac's single-instance mutex (App.Main): ask the user to close RPMac before upgrade/uninstall.
AppMutex=RPMac_singleton_v1
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
OutputBaseFilename=RPMac-v{#MyAppVersion}-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Writes %TEMP%\Setup Log *.txt, with PawnIO's exit code if its install fails.
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
T2Group=T2 Mac (2018-2020):
PawnIOTask=Install PawnIO {#PawnIOVersion}, the free signed driver RPMac needs on this Mac (third-party, by namazso - pawnio.eu)
InstallingPawnIO=Installing PawnIO (kernel driver)...
PawnIOLaunchFailed=PawnIO's installer could not be started: %1%n%nRPMac will still be installed. On a T2 Mac it stays read-only until PawnIO is installed from https://pawnio.eu.
PawnIOFailed=PawnIO's installer failed (exit code %1: %2)%n%nRPMac will still be installed. On a T2 Mac it stays read-only until PawnIO is installed from https://pawnio.eu.
PawnIOPendingRestart=PawnIO could not be installed because an earlier PawnIO removal is still waiting for a restart (exit code 1072).%n%nRestart Windows, then run this installer again.
PawnIODownloadFailed=PawnIO could not be downloaded: %1%n%nAbort: go back. Retry: try again. Ignore: install RPMac without PawnIO.
PawnIOTooOld=PawnIO %1 is installed, but RPMac's T2 module expects PawnIO 2.0 or newer. If RPMac stays read-only, update PawnIO from https://pawnio.eu.
StartupTaskFailed=The "start with Windows" task could not be created (%1). You can turn on "Start with Windows" inside RPMac instead.

[Tasks]
; Only on T2 Macs without PawnIO, on 64-bit Windows 10 1809+ (the setup refuses anything older).
Name: "pawnio"; Description: "{cm:PawnIOTask}"; GroupDescription: "{cm:T2Group}"; MinVersion: 10.0.17763; Check: ShouldOfferPawnIO
; Same scheduled task as RPMac's "Start with Windows" toggle. Checked only on the first install,
; so an upgrade never overrides what the user chose inside the app.
Name: "startup"; Description: "{cm:AutoStartProgram,{#MyAppName}}"; GroupDescription: "{cm:AutoStartProgramGroupDescription}"; Flags: checkedonce

[Files]
#ifndef PawnIODownload
; First in the list: with SolidCompression, ExtractTemporaryFile has to decompress everything before it.
Source: "{#PawnIOSetupSrc}"; DestName: "PawnIO_setup.exe"; Hash: "{#PawnIOSha256}"; Flags: dontcopy noencryption
#endif
; The same files as the release zip, listed one by one (a wildcard also shipped build\smccore.log).
Source: "{#BuildDir}\RPMac.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\RPMac.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\smccore.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\inpout32.dll"; DestDir: "{app}"; Flags: ignoreversion
; Must be the SIGNED module from PawnIO.Modules 0.2.12 (the regular PawnIO rejects the unsigned
; test build), so the compiler refuses any other file. Update the hash when the module is re-signed.
Source: "{#BuildDir}\AppleT2Smc.bin"; DestDir: "{app}"; Hash: "127dc1d3e58b41d2397772669736e9f366a0908fc2ea16745ddf74e685766821"; Flags: ignoreversion
Source: "{#DistDir}\READ ME - RPMac.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoDir}\NOTICE"; DestDir: "{app}"; Flags: ignoreversion
; AppleT2Smc.bin is LGPL-2.1, which asks for its text to travel with it.
Source: "{#DistDir}\COPYING.LGPL-2.1"; DestDir: "{app}"; Flags: ignoreversion
#if defined(ShipPawnIOSetupInApp) && !defined(PawnIODownload)
Source: "{#PawnIOSetupSrc}"; DestDir: "{app}"; DestName: "PawnIO_setup.exe"; Flags: ignoreversion; Check: IsT2Mac
#endif

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; No {autostartup} shortcut: RPMac.exe is requireAdministrator and Windows blocks elevation from the
; Startup folders and Run keys at logon. The "startup" task registers a scheduled task instead.

[Registry]
; Appended (not prepended) to the machine PATH so smccore.exe works from any terminal.
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsAddPath(ExpandConstant('{app}'))

[Run]
; postinstall entries run as the original (non-elevated) user by default, and RPMac.exe is
; requireAdministrator: that fails with error 740, or with shellexec asks for UAC a second time.
; runascurrentuser starts it with Setup's own elevated rights instead. Skipped if a restart is pending.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall nowait skipifsilent runascurrentuser

[UninstallRun]
; The logon task, whether this setup or RPMac's own toggle created it. PawnIO itself is left
; installed: other tools (FanControl, LibreHardwareMonitor) share it, and it has its own uninstaller.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN RPMac /F"; Flags: runhidden; RunOnceId: "DelRPMacTask"
; The InpOut driver that inpout32.dll registered on RPMac's first run (auto-start, loads at every
; boot). Nothing needs it once RPMac is gone, and anti-cheats such as Vanguard block it. Any other
; InpOut-based program registers it again by itself the next time it runs elevated.
Filename: "{sys}\sc.exe"; Parameters: "stop inpoutx64"; Flags: runhidden; RunOnceId: "StopInpOut"
Filename: "{sys}\sc.exe"; Parameters: "delete inpoutx64"; Flags: runhidden; RunOnceId: "DelInpOut"

[UninstallDelete]
; smccore.exe appends smccore.log next to itself.
Type: files; Name: "{app}\smccore.log"
Type: files; Name: "{sysnative}\drivers\inpoutx64.sys"

[Code]
const
  EnvKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  BiosKey = 'HARDWARE\DESCRIPTION\System\BIOS';
  PawnIOUninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO';
  ERROR_SUCCESS_REBOOT_REQUIRED = 3010;
  ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
  TASK_CREATE_OR_UPDATE = 6;
  TASK_LOGON_INTERACTIVE_TOKEN = 3;

var
  T2Known, T2Result: Boolean;
  PawnIORestartNeeded: Boolean;
  PawnIOSkipped: Boolean;
#ifdef PawnIODownload
  DownloadPage: TDownloadWizardPage;
#endif

{ ---------- T2 detection ---------- }

function HasPrefix(const Prefix, S: String): Boolean;
begin
  Result := CompareText(Copy(S, 1, Length(Prefix)), Prefix) = 0;
end;

{ Apple's T2 Macs (support.apple.com/103265), by the model identifier that Boot Camp puts in
  SMBIOS SystemProductName. Same list as T2_MODEL_PREFIXES in Smc.cs. }
function IsT2Model(const Model: String): Boolean;
begin
  Result := HasPrefix('MacBookPro15,', Model) or HasPrefix('MacBookPro16,', Model) or
            HasPrefix('MacBookAir8,', Model) or HasPrefix('MacBookAir9,', Model) or
            HasPrefix('Macmini8,', Model) or HasPrefix('iMac20,', Model) or
            HasPrefix('iMacPro1,', Model) or HasPrefix('MacPro7,', Model);
end;

function IsT2Mac: Boolean;
var
  Model, Force: String;
begin
  if not T2Known then begin
    Model := '';
    if not RegQueryStringValue(HKLM, BiosKey, 'SystemProductName', Model) then
      Model := '';
    Model := Trim(Model);
    { /PAWNIO=yes offers PawnIO anyway (unusual SMBIOS, testing); /PAWNIO=no never offers it }
    Force := LowerCase(ExpandConstant('{param:PAWNIO|auto}'));
    if Force = 'yes' then
      T2Result := True
    else if Force = 'no' then
      T2Result := False
    else
      T2Result := IsT2Model(Model);
    if T2Result then
      Log('Model "' + Model + '": T2 Mac, PawnIO is needed')
    else
      Log('Model "' + Model + '": PawnIO not needed');
    T2Known := True;
  end;
  Result := T2Result;
end;

{ ---------- PawnIO detection ---------- }

{ PawnIO_setup.exe is a 64-bit program and registers Uninstall\PawnIO in the 64-bit view; this
  setup is 32-bit, so read both views explicitly. That entry is exactly what PawnIO's own setup
  checks. The Services\PawnIO key is NOT used: PawnIO's uninstaller leaves it behind (with the
  driver in the DriverStore), so it would hide the PawnIO task on a Mac that no longer has PawnIO. }
function IsPawnIOInstalled: Boolean;
begin
  Result := False;
  if IsWin64 then
    Result := RegKeyExists(HKLM64, PawnIOUninstallKey);
  if not Result then
    Result := RegKeyExists(HKLM32, PawnIOUninstallKey);
end;

function PawnIODisplayVersion: String;
begin
  Result := '';
  if IsWin64 then
    if RegQueryStringValue(HKLM64, PawnIOUninstallKey, 'DisplayVersion', Result) then
      Exit;
  if not RegQueryStringValue(HKLM32, PawnIOUninstallKey, 'DisplayVersion', Result) then
    Result := '';
end;

function ShouldOfferPawnIO: Boolean;
begin
  Result := IsWin64 and IsT2Mac and not IsPawnIOInstalled;
end;

procedure WarnIfPawnIOTooOld;
var
  V: String;
  Installed: Int64;
begin
  V := PawnIODisplayVersion;
  Log('PawnIO already installed, version "' + V + '": not installing it again');
  if (V <> '') and StrToVersion(V, Installed) then
    if ComparePackedVersion(Installed, PackVersionComponents(2, 0, 0, 0)) < 0 then
      SuppressibleMsgBox(FmtMessage(CustomMessage('PawnIOTooOld'), [V]), mbInformation, MB_OK, IDOK);
end;

{ ---------- PawnIO install ---------- }

procedure InstallPawnIO;
var
  SetupExe, Msg: String;
  ResultCode: Integer;
begin
  SetupExe := ExpandConstant('{tmp}\PawnIO_setup.exe');
#ifndef PawnIODownload
  ExtractTemporaryFile('PawnIO_setup.exe');
#endif
  if not FileExists(SetupExe) then begin
    Log('PawnIO_setup.exe not available, PawnIO not installed');
    Exit;
  end;
  Msg := '';
  WizardForm.StatusLabel.Caption := CustomMessage('InstallingPawnIO');
  WizardForm.ProgressGauge.Style := npbstMarquee;
  { A cancel now would roll RPMac back but leave a half-finished driver install. }
  WizardForm.CancelButton.Enabled := False;
  try
    { -silent: no UI at all, even on error. Since 2.2.0 the exit code is a Win32 error code and
      3010 (ERROR_SUCCESS_REBOOT_REQUIRED) means it worked but needs a restart. Setup is elevated,
      so Exec can start this requireAdministrator exe directly. }
    if not Exec(SetupExe, '-install -silent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Msg := FmtMessage(CustomMessage('PawnIOLaunchFailed'), [SysErrorMessage(ResultCode)])
    else begin
      Log(Format('PawnIO_setup.exe -install -silent exited with code %d', [ResultCode]));
      if ResultCode = ERROR_SUCCESS_REBOOT_REQUIRED then
        PawnIORestartNeeded := True
      else if ResultCode = ERROR_SERVICE_MARKED_FOR_DELETE then begin
        PawnIORestartNeeded := True;
        Msg := CustomMessage('PawnIOPendingRestart');
      end else if ResultCode <> 0 then
        Msg := FmtMessage(CustomMessage('PawnIOFailed'), [IntToStr(ResultCode), SysErrorMessage(ResultCode)]);
    end;
  finally
    WizardForm.ProgressGauge.Style := npbstNormal;
    WizardForm.CancelButton.Enabled := True;
  end;
  if Msg <> '' then
    SuppressibleMsgBox(Msg, mbError, MB_OK, IDOK);
end;

{ Called by Setup inside the install step, after files/registry and before [Run] and ssPostInstall.
  PawnIO runs at ssInstall, before that point, so its 3010 turns into the normal restart prompt
  (which also skips the "Launch RPMac" checkbox). }
function NeedRestart: Boolean;
begin
  Result := PawnIORestartNeeded;
end;

{ ---------- Start with Windows ---------- }

function XmlEscape(S: String): String;
begin
  StringChangeEx(S, '&', '&amp;', True);
  StringChangeEx(S, '<', '&lt;', True);
  StringChangeEx(S, '>', '&gt;', True);
  StringChangeEx(S, '"', '&quot;', True);
  StringChangeEx(S, '''', '&apos;', True);
  Result := S;
end;

{ The same definition as Startup.Enable in src/gui/App.cs (logon trigger, highest privileges,
  allowed on battery, priority 5, no time limit), so RPMac's toggle shows it as on and its
  FixPriority() leaves it alone. Registered through the Task Scheduler COM API, which takes the
  XML as a string: no temp file, no UTF-16 file encoding to get right. }
procedure RegisterStartupTask;
var
  Service, Folder: Variant;
  Xml: String;
begin
  Xml :=
    '<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' +
    '<RegistrationInfo><Description>RPMac fan control</Description></RegistrationInfo>' +
    '<Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>' +
    '<Principals><Principal id="Author"><LogonType>InteractiveToken</LogonType>' +
    '<RunLevel>HighestAvailable</RunLevel></Principal></Principals>' +
    '<Settings>' +
    '<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>' +
    '<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>' +
    '<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>' +
    '<Priority>5</Priority>' +
    '<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>' +
    '<Enabled>true</Enabled>' +
    '</Settings>' +
    '<Actions Context="Author"><Exec><Command>' +
    XmlEscape(ExpandConstant('{app}\{#MyAppExeName}')) +
    '</Command></Exec></Actions>' +
    '</Task>';
  try
    Service := CreateOleObject('Schedule.Service');
    Service.Connect();
    Folder := Service.GetFolder('\');
    Folder.RegisterTask('RPMac', Xml, TASK_CREATE_OR_UPDATE, '', '', TASK_LOGON_INTERACTIVE_TOKEN);
    Log('Registered the RPMac logon task');
  except
    Log('Registering the RPMac logon task failed: ' + GetExceptionMessage);
    SuppressibleMsgBox(FmtMessage(CustomMessage('StartupTaskFailed'), [GetExceptionMessage]), mbInformation, MB_OK, IDOK);
  end;
end;

{ "Start with Windows" unticked, but the user already had it on in a portable copy: their task still
  launches that old RPMac.exe. Keep their choice and point it at this install instead. }
procedure RepointStartupTask;
var
  Service, Folder, Task: Variant;
  OldExe: String;
begin
  try
    Service := CreateOleObject('Schedule.Service');
    Service.Connect();
    Folder := Service.GetFolder('\');
    Task := Folder.GetTask('RPMac');   { raises when there is no such task }
    OldExe := RemoveQuotes(Task.Definition.Actions.Item(1).Path);
    if CompareText(OldExe, ExpandConstant('{app}\{#MyAppExeName}')) <> 0 then begin
      Log('Existing RPMac logon task runs "' + OldExe + '": pointing it at this install');
      RegisterStartupTask;
    end;
  except
    { no task, or one we can't read: nothing to do }
  end;
end;

{ ---------- PATH ---------- }

function NeedsAddPath(Param: String): Boolean;
var
  OrigPath: String;
begin
  if not RegQueryStringValue(HKLM, EnvKey, 'Path', OrigPath) then begin
    Result := True;
    Exit;
  end;
  { look for the path with leading and trailing semicolon, ignoring case }
  Result := Pos(';' + UpperCase(Param) + ';', ';' + UpperCase(OrigPath) + ';') = 0;
end;

procedure RemoveFromPath(const Dir: String);
var
  Path, S: String;
  P: Integer;
begin
  if not RegQueryStringValue(HKLM, EnvKey, 'Path', Path) then
    Exit;
  S := ';' + Path + ';';
  P := Pos(';' + UpperCase(Dir) + ';', UpperCase(S));
  if P = 0 then
    Exit;
  Delete(S, P, Length(Dir) + 1);
  Path := Copy(S, 2, Length(S) - 2);
  if RegWriteExpandStringValue(HKLM, EnvKey, 'Path', Path) then
    Log('Removed ' + Dir + ' from the system PATH');
end;

{ ---------- Event functions ---------- }

procedure InitializeWizard;
begin
  if IsWin64 and IsT2Mac and IsPawnIOInstalled then
    WarnIfPawnIOTooOld;
#ifdef PawnIODownload
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
#endif
end;

#ifdef PawnIODownload
{ Download on leaving the Ready page, as in Inno's CodeDownloadFiles.iss. Add's third argument is
  the required SHA-256: Download raises an exception if the file doesn't match. }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Retry: Boolean;
begin
  Result := True;
  if (CurPageID = wpReady) and WizardIsTaskSelected('pawnio') and not IsPawnIOInstalled then begin
    PawnIOSkipped := False;
    DownloadPage.Clear;
    DownloadPage.Add('{#PawnIOUrl}', 'PawnIO_setup.exe', '{#PawnIOSha256}');
    DownloadPage.Show;
    try
      Retry := True;
      while Retry do begin
        Retry := False;
        try
          DownloadPage.Download; // saves it as {tmp}\PawnIO_setup.exe
        except
          if DownloadPage.AbortedByUser then
            Result := False
          else
            case SuppressibleMsgBox(FmtMessage(CustomMessage('PawnIODownloadFailed'), [GetExceptionMessage]),
                                    mbError, MB_ABORTRETRYIGNORE, IDIGNORE) of
              IDABORT: Result := False;
              IDRETRY: Retry := True;
            else
              PawnIOSkipped := True;
            end;
        end;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;
#endif

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Exceptions at ssInstall would abort Setup, so nothing may escape from here. }
  if (CurStep = ssInstall) and WizardIsTaskSelected('pawnio') and not PawnIOSkipped
     and not IsPawnIOInstalled then
    try
      InstallPawnIO;
    except
      Log('Installing PawnIO raised: ' + GetExceptionMessage);
      SuppressibleMsgBox(FmtMessage(CustomMessage('PawnIOLaunchFailed'), [GetExceptionMessage]), mbError, MB_OK, IDOK);
    end;
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('startup') then
      RegisterStartupTask
    else
      RepointStartupTask;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveFromPath(ExpandConstant('{app}'));
end;
