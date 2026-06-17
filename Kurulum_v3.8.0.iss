[Setup]
AppName=Klyze
AppVersion=3.8.0
AppPublisher=Klyze.gg
DefaultDirName={autopf}\Klyze
DefaultGroupName=Klyze
OutputDir=C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0
OutputBaseFilename=Klyze_v3.8.0_Setup
SetupIconFile=C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0\icon.ico
UninstallDisplayIcon={app}\Klyze.exe
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaustu kisa yolu olustur"; GroupDescription: "Kisa Yollar:"

[Files]
Source: "C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0\Klyze.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0\config.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0\icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0\*.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0\ranklar\*"; DestDir: "{app}\ranklar"; Flags: ignoreversion recursesubdirs
Source: "C:\Users\omery\Music\klyze kayak kodları\Kurulum_v3.8.0\data\*"; DestDir: "{app}\data"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Klyze"; Filename: "{app}\Klyze.exe"
Name: "{autodesktop}\Klyze"; Filename: "{app}\Klyze.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Klyze.exe"; Description: "Klyze'yi baslat"; Flags: postinstall nowait skipifsilent shellexec runascurrentuser

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM Klyze.exe"; Flags: runhidden
