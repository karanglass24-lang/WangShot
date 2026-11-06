; 왕샷 설치 프로그램 (Inno Setup 6)
;   ISCC.exe installer\wangshot.iss
; 먼저 빌드: dotnet build src\Greenshot\Greenshot.csproj -c Release
#define AppName "왕샷"
#define AppVersion "1.1.0"
#define AppExe "WangShot.exe"
#define BuildDir "..\src\Greenshot\bin\Release\net480"

[Setup]
; 이 GUID 는 왕샷 전용이에요. 바꾸면 업데이트가 아니라 새 프로그램으로 설치돼요.
AppId={{6B1E4C52-0F7A-4D7E-9C3B-5A8D2F41E7B9}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=karanglass24-lang
AppPublisherURL=https://github.com/karanglass24-lang/WangShot
AppSupportURL=https://github.com/karanglass24-lang/WangShot/issues
AppUpdatesURL=https://github.com/karanglass24-lang/WangShot/releases
DefaultDirName={autopf}\WangShot
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\Greenshot\icons\applicationIcon\icon.ico
LicenseFile=..\LICENSE
OutputDir=..\..\release
OutputBaseFilename=WangShot-{#AppVersion}-setup
Compression=lzma2/ultra
SolidCompression=yes
; 관리자 권한 없이 내 계정에만 설치(기본), 설치할 때 모든 사용자용으로 바꿀 수도 있어요
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Windows 시작할 때 왕샷 자동 실행"; GroupDescription: "시작 옵션:"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autoprograms}\{#AppName} 편집기 열기"; Filename: "{app}\{#AppExe}"; Parameters: "--editor"; IconFilename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WangShot"; ValueData: """{app}\{#AppExe}"" --autostart"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; 자동 업데이트(/SILENT /relaunch=1)로 설치했을 때는 끝나고 왕샷을 다시 실행해요
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser; Check: ShouldRelaunch

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--exit"; Flags: runhidden skipifdoesntexist; RunOnceId: "ExitWangShot"

[Code]
function ShouldRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:relaunch|0}') = '1';
end;
