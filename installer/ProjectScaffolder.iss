#ifndef AppVersion
  #define AppVersion "2.0.0-alpha.2"
#endif
#ifndef SourceDir
  #error SourceDir must point to the self-contained publish directory.
#endif
#ifndef OutputDir
  #error OutputDir must point to the installer artifact directory.
#endif

[Setup]
AppId={{D9557332-AB9E-4AC1-BE3A-83DC83187CA4}
AppName=Project Scaffolder
AppVersion={#AppVersion}
AppVerName=Project Scaffolder {#AppVersion}
DefaultDirName={localappdata}\Programs\ProjectScaffolder
DefaultGroupName=Project Scaffolder
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=ProjectScaffolder-Setup-{#AppVersion}
SetupIconFile=..\src\ProjectScaffolder.Editor\Assets\ProjectScaffolder.ico
UninstallDisplayIcon={app}\ProjectScaffolder.Editor.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
VersionInfoVersion=2.0.0.2
VersionInfoDescription=Project Scaffolder per-user installer

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\SeedTemplates\3ds-max-template.json"

[Icons]
Name: "{userprograms}\Project Scaffolder\Project Scaffolder"; Filename: "{app}\ProjectScaffolder.Editor.exe"; WorkingDir: "{app}"
Name: "{userprograms}\Project Scaffolder\Uninstall Project Scaffolder"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\ProjectScaffolder"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ProjectScaffolder"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\ContextMenus\ProjectScaffolder.Background"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\ContextMenus\ProjectScaffolder.SelectedFolder"; Flags: uninsdeletekey

[Run]
Filename: "{app}\ProjectScaffolder.Editor.exe"; Parameters: "--register-explorer-menu"; StatusMsg: "Registering the Explorer menu..."; Flags: runhidden waituntilterminated
Filename: "{app}\ProjectScaffolder.Editor.exe"; Description: "Launch Project Scaffolder"; Flags: postinstall nowait skipifsilent unchecked

[Code]
var
  RemoveUserData: Boolean;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveUserData :=
      SuppressibleMsgBox(
        'Also remove your Project Scaffolder templates, settings, logs, and Trash?' + #13#10 + #13#10 +
        'Choosing Yes permanently deletes user-created templates and attached files. Generated projects are not affected.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES;

  if (CurUninstallStep = usPostUninstall) and RemoveUserData then
    DelTree(ExpandConstant('{localappdata}\ProjectScaffolder'), True, True, True);
end;
