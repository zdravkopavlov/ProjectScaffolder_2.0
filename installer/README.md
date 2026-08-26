# Project Scaffolder installer

The initial release uses Inno Setup 6 to produce a per-user, self-contained Windows x64 installer.

Build from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Build-Installer.ps1
```

The build script publishes the editor and runner into `artifacts\installer\publish`, then creates:

```text
artifacts\installer\ProjectScaffolder-Setup-<version>.exe
```

Use `-Version` to set the application and installer display version. Use `-InnoCompiler` only when `ISCC.exe` is not installed in a standard Inno Setup 6 location.

The installer:

- installs without elevation under `%LOCALAPPDATA%\Programs\ProjectScaffolder`;
- includes the .NET Windows x64 runtime;
- creates Project Scaffolder and uninstall Start menu shortcuts;
- seeds bundled templates only when the corresponding user template does not already exist;
- refreshes the current user's classic Explorer menu after installation or repair;
- removes binaries, shortcuts, and Project Scaffolder-owned Explorer keys on uninstall;
- preserves `%LOCALAPPDATA%\ProjectScaffolder` templates, settings, Trash, and logs by default on uninstall;
- offers a default-No uninstall prompt to remove that local user data without affecting generated projects.
