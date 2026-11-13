; Definisi installer WinToolbar (Inno Setup 6).
; Jangan dijalankan langsung; pakai build-installer.ps1 yang mengisi AppVersion, SourceDir, dan OutputDir.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
; AppId menandai aplikasi ini untuk upgrade dan uninstall. Jangan diganti.
AppId={{7C0B6E0A-52D4-4B7E-9A3F-2E1D8C5A4F61}
AppName=WinToolbar
AppVersion={#AppVersion}
; Per pengguna, tanpa hak admin.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\WinToolbar
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Sama dengan mutex single-instance di App.xaml.cs: setup dan uninstall
; meminta aplikasi ditutup dulu kalau masih jalan.
AppMutex=WinToolbar.SingleInstance
UninstallDisplayIcon={app}\WinToolbar.exe
OutputDir={#OutputDir}
OutputBaseFilename=WinToolbar-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "autostart"; Description: "Jalankan WinToolbar otomatis saat Windows dinyalakan"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\WinToolbar"; Filename: "{app}\WinToolbar.exe"

[Registry]
; Nilai yang sama dengan yang dipakai menu "Jalan saat startup" di aplikasi (Startup/AutoStart.cs).
; Baris pertama memastikan nilainya terhapus saat uninstall walaupun diaktifkan dari dalam aplikasi.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "WinToolbar"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WinToolbar"; ValueData: """{app}\WinToolbar.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\WinToolbar.exe"; Description: "Jalankan WinToolbar sekarang"; Flags: nowait postinstall skipifsilent
