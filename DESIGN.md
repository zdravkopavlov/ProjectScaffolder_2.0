# Project Scaffolder 2.0 — Product and Technical Design

| Field | Value |
| --- | --- |
| Document status | Living design — approved baseline and JSON contract |
| Baseline status | Approved for implementation |
| Document version | 1.7 |
| Last updated | 2026-08-24 |
| Target platform | Windows 11 x64 |

## 1. Purpose of this document

This is the living, authoritative design document for Project Scaffolder 2.0. It records the product behavior, technical direction, data contracts, safety rules, implementation milestones, open questions, and important decision history.

The implementation must follow the approved portions of this document. When the implementation and this document disagree, the disagreement must be resolved explicitly rather than allowing either one to become silently outdated.

### 1.1 Maintenance rules

- Update this document whenever a product, architecture, file-format, installation, or workflow decision changes.
- Keep confirmed decisions separate from proposals and open questions.
- Mark completed milestone work in the implementation checklist.
- Record meaningful reversals or tradeoffs in the decision log.
- Do not use this document as a diary of routine code changes.
- Do not begin implementation until the initial design baseline is reviewed and approved.
- A future `README.md` will document installation and everyday use for end users. It will not replace this design document.
- If the implementation checklist becomes too large, move it to `ROADMAP.md` while keeping architectural milestones here.

### 1.2 Decision status vocabulary

- **Confirmed:** Explicitly agreed and part of the design baseline.
- **Proposed:** Recommended direction that still needs review.
- **Open:** A decision has not been made.
- **Deferred:** Intentionally excluded from the initial release but kept in the design for future compatibility.

## 2. Product summary

Project Scaffolder is a Windows desktop utility for defining reusable project structures and applying them directly inside the current folder from Windows Explorer.

The installed application adds a cascading classic Explorer context menu similar to:

```text
New Project
├─ New Max Project
├─ New Photogrammetry Project
├────────────────────────────
└─ Editor
```

The user creates and names an empty project folder, enters it, right-clicks its background, and chooses a project template. The selected structure is generated directly inside that folder without a project-name prompt, confirmation dialog, or success popup.

An editor provides a visual way to create templates containing folders and optional files, preview and validate them, and choose which templates appear in the Explorer submenu.

Every scaffolded project receives a stable `.projectID.json` metadata file. The file contains a unique project identifier, creation timestamp, and provenance needed for possible future database integration.

## 3. Product principles

1. **Preserve the proven workflow.** The user prepares and names the destination folder before invoking the tool.
2. **Be silent on success.** Normal project creation requires no additional interaction.
3. **Be safe by construction.** A malformed template must not write outside the target folder or overwrite unrelated data.
4. **Be generic.** MXP files and similar application-specific files are represented through the same general file-template system.
5. **Make Explorer integration optional and reversible.** Templates can be added to or removed from the menu without deleting them.
6. **Keep project identity stable.** Moving or renaming a project must not change its ID.
7. **Prefer maintainable Windows-native technology.** Avoid complexity that does not improve the user experience.
8. **Design for future growth without building speculative systems.** Future modern-menu and database support should be possible, but neither should dominate the first release.

## 4. Goals and non-goals

### 4.1 Initial-release goals

- Provide an installation executable and clean uninstallation.
- Install and run without requiring a separately installed .NET runtime.
- Register a per-user classic Explorer cascading menu.
- Support invocation on a folder background and a selected folder.
- Apply a chosen template directly inside the target folder.
- Provide a friendly visual template editor.
- Support nested folders and optional files.
- Support templated text file names and contents.
- Support copying existing files into a generated project.
- Allow each template to be shown in or hidden from the Explorer menu.
- Generate `.projectID.json` with a UUIDv7 project identifier.
- Validate templates before saving and again before generation.
- Avoid partial project creation where recovery is possible.
- Preserve user templates during normal application upgrades and uninstallation.
- Include automated tests for the template engine and path-safety rules.
- Ship polished starter templates for 3ds Max, photogrammetry, and generic 3D production.

### 4.2 Initial-release non-goals

- A cloud account or synchronization service.
- A central project database.
- Multi-user collaboration.
- Cross-platform support.
- Executing arbitrary template scripts or post-generation hooks.
- A general plugin SDK.
- Editing the Windows Registry manually as part of normal use.
- Native Windows 11 compact-menu integration in the first implementation.
- Automatically updating existing projects when a template changes.
- Treating every file format as a specialized generator.

## 5. Terminology

| Term | Meaning |
| --- | --- |
| Project | A directory that has been scaffolded and normally contains `.projectID.json`. |
| Template | A reusable definition of folders, files, variables, and generation behavior. |
| Template ID | A stable identifier belonging to a template, independent of its display name. |
| Project ID | A unique identifier generated once for a scaffolded project. |
| Target folder | The existing folder into which the selected template is applied. |
| Structure node | A folder or file in a template's visual tree. |
| Registration | Making a template available in the Explorer submenu. |
| Runner | The small, noninteractive executable invoked from Explorer. |
| Editor | The desktop UI used to manage templates and integration settings. |
| Shell integration | The registry entries and commands that connect the application to Explorer. |

## 6. Confirmed product decisions

| ID | Decision | Status |
| --- | --- | --- |
| D-001 | Project structures are generated inside the current, already existing folder. | Confirmed |
| D-002 | The normal workflow has no project-name prompt or confirmation dialog. | Confirmed |
| D-003 | Successful generation produces no popup. | Confirmed |
| D-004 | The folder's existing name supplies the project-name variable. | Confirmed |
| D-005 | Explorer exposes one `New Project` flyout containing enabled templates, a separator, and `Editor`. | Confirmed |
| D-006 | The classic Explorer context menu is the primary integration. | Confirmed |
| D-007 | Modern Windows 11 context-menu support may be added later. | Deferred |
| D-008 | The editor can visually build nested folder structures and add files. | Confirmed |
| D-009 | Each project type can be registered or unregistered from the flyout without deleting its template. | Confirmed |
| D-010 | Project metadata uses the exact canonical filename `.projectID.json`. | Confirmed |
| D-011 | Project IDs use UUID version 7. | Confirmed |
| D-012 | Project metadata records creation time and template provenance for future database use. | Confirmed |
| D-013 | C# and modern .NET are the primary implementation technologies. | Confirmed |
| D-014 | Python will not be used for the main application. | Confirmed |
| D-015 | Specialized text formats such as MXP should be supported through the generic file-template model. | Confirmed |
| D-016 | Windows 11 x64 is the sole supported operating-system target; Windows 10 compatibility is out of scope. | Confirmed |
| D-017 | The final product name is `Project Scaffolder`. | Confirmed |
| D-018 | Every scaffolded project receives `.projectID.json`; templates cannot opt out. | Confirmed |
| D-019 | `.projectID.json` receives the Windows Hidden attribute by default. | Confirmed |
| D-020 | An existing project may rerun its original template, but invoking a different template is a blocking error. | Confirmed |
| D-021 | Existing generated files are skipped by default, while existing folders are safely reused. | Confirmed |
| D-022 | Initial structure nodes include folders, templated text files, and attached asset files copied verbatim. A text file may have empty content, so a separate empty-file node is unnecessary. | Confirmed |
| D-023 | Project Scaffolder uses a per-user, non-elevated installation under `%LOCALAPPDATA%\Programs\ProjectScaffolder`; user templates and settings use `%LOCALAPPDATA%\ProjectScaffolder`. | Confirmed |
| D-024 | JSON contract version 1 is defined jointly by `JSON-CONTRACT.md` and the strict Draft 2020-12 schemas under `schemas`. | Confirmed |
| D-025 | Explorer enablement and order are local library state and are not exported inside templates; new/imported templates begin disabled. | Confirmed |
| D-026 | Version 1 supports `skip`, explicit `overwrite`, and `fail` file-collision policies, with `skip` as the default. | Confirmed |
| D-027 | Template and node identities use stable UUIDv7 values; version 1 variables are lowercase and case-sensitive. | Confirmed |
| D-028 | The product is an artist-first generic scaffolder: 3ds Max is a polished starter use case, not a specialized engine or editor mode. | Confirmed |
| D-029 | The editor follows the Windows light/dark theme by default and offers explicit Light and Dark overrides. | Confirmed |
| D-030 | Explorer registration is toggled directly on each template row and applies immediately; it is not part of the template's unsaved document state. | Confirmed |
| D-031 | Template saving belongs in the template-list footer with a visible dirty-state indicator and `Ctrl+S` support. | Confirmed |
| D-032 | The editor exposes one clearly named `Test template...` workflow instead of separate ambiguous Preview and Test Generate buttons. | Confirmed |
| D-033 | The runner writes a compact structured log for successful and failed runs, retaining at most 100 files and 30 days. | Confirmed |
| D-034 | The editor implementation uses WPF with an editor-local mutable view-model layer; Core contracts remain UI-independent and immutable. | Confirmed |
| D-035 | Removing a saved template moves its complete package into an application-owned Trash folder instead of permanently deleting it. | Confirmed |

## 7. User workflows

### 7.1 Installation

1. The user launches the installer executable.
2. The installer installs the editor, runner, icons, default templates, and uninstaller.
3. The installer registers the stable `New Project` classic Explorer menu for the current user.
4. Bundled templates are copied only when they do not replace user-owned templates.
5. The application is available from the Start menu and through `New Project > Editor`.

Normal per-user installation should not require elevation. A future all-users installation mode may require administrator approval.

### 7.2 Creating a project

1. The user creates an empty folder in Explorer.
2. The user gives that folder the intended project name.
3. The user opens the folder.
4. The user right-clicks the folder background.
5. The user selects `New Project` and then a template.
6. The runner resolves the current folder and selected template.
7. The runner preflights the entire generation plan.
8. The runner creates folders and files in place.
9. The runner creates `.projectID.json` after the rest of the required structure succeeds.
10. The runner exits silently.

The same semantics apply when the command is invoked directly on a selected folder: that selected folder is the target.

### 7.3 Generation failure

If validation or generation fails:

1. The runner stops.
2. It rolls back only artifacts created by the current invocation when that can be done safely.
3. It never deletes or restores over pre-existing user content.
4. It records diagnostic details in a log.
5. It displays one concise error dialog containing the cause and a way to locate additional details.

### 7.4 Creating a template

1. The user opens the editor.
2. The user selects `New Template`.
3. The user names the template.
4. The user builds a hierarchy using folder and file nodes.
5. The user edits file properties or text content.
6. The editor validates the template continuously.
7. The user previews the resolved result with a sample target folder.
8. The user saves the template.
9. The user optionally enables it in Explorer.

### 7.5 Importing an existing structure

1. The user selects `Import Folder`.
2. The user chooses an existing project directory.
3. The editor reads its hierarchy.
4. The user chooses whether files should be imported as editable text, attached verbatim, or omitted.
5. Known occurrences of the source folder name and path may be offered as variable substitutions, but replacements require review.
6. The imported result opens as an unsaved template for validation and editing.

### 7.6 Enabling or disabling a menu entry

1. The user checks or clears the Explorer checkbox on that template's row.
2. The application immediately updates local library state and refreshes the submenu registration.
3. The checkbox becomes green when enabled and gray when disabled, with a text tooltip and accessible label identifying its purpose.
4. Disabling a template does not save, delete, or modify its structure.

## 8. Explorer integration

### 8.1 Classic menu — initial implementation

The initial release uses static cascading-menu registration under the current user's `Software\Classes` registry area. It must support:

- `Directory\Background\shell` for the folder currently open in Explorer.
- `Directory\shell` for a selected directory.
- One stable top-level `New Project` verb.
- One child command per enabled template.
- A separator before the `Editor` command.
- An application icon on the top-level entry, subject to Explorer limitations.

The editor owns registration updates. The runner never modifies registration while generating a project.

Each top-level verb stores `ExtendedSubCommandsKey` as a `REG_SZ` value pointing to an application-owned command set under `Directory\ContextMenus`; it is not a nested key beneath the verb. Background and selected-folder verbs use separate command sets because their target placeholders differ.

Command construction must quote executable paths, target paths, and template identifiers correctly. The runner must not treat user-controlled path text as a shell command.

### 8.2 Registration model

Registration is represented in application data rather than inferred solely from the Registry. Each template contains a stable ID; the per-user template-library document stores Explorer enablement and menu order separately.

The editor generates the current submenu from enabled templates. Template names may change without changing template identity. A registration refresh must remove stale application-owned child entries while preserving unrelated Registry content.

The registration layer must expose a health check that distinguishes:

- Registered and current.
- Registered but out of date.
- Partially registered.
- Not registered.
- Registration unavailable or damaged.

### 8.3 Ordering

**Proposed default:** Enabled templates appear in a user-controlled order. `Editor` always appears last after a separator.

### 8.4 Modern Windows 11 menu

Modern compact-menu support is deferred. The architecture must allow a future shell provider based on `IExplorerCommand` without changing template files or generation behavior.

A future native extension must perform no template generation inside Explorer's process. It may enumerate a small cached menu index and launch the runner or editor. Expensive work must remain outside Explorer.

## 9. Editor experience

### 9.1 Confirmed primary layout

```text
┌────────────────────┬──────────────────────────────┬──────────────────────┐
│ Templates          │ Project Structure            │ Selected Item        │
│                    │                              │                      │
│ Max Project     [✓]│ 📁 {{project_name}}          │ Name                 │
│ Photogrammetry  [✓]│   ├─ 📁 SCENES               │ Node type            │
│ Generic 3D      [ ]│   ├─ 📁 RENDERS              │ Content/source       │
│                    │   ├─ 📁 ASSETS               │ Encoding             │
│                    │   └─ 📄 {{project_name}}.mxp │ Collision behavior   │
│ Unsaved changes    │                              │                      │
│ [Save template]    │                              │                      │
├────────────────────┴──────────────────────────────┴──────────────────────┤
│ Validation / status                                      [Test template]│
└──────────────────────────────────────────────────────────────────────────┘
```

The template list owns selection, Explorer registration, dirty state, and saving. The structure pane owns hierarchy editing. The selected-item pane owns properties and text contents. This separation keeps template-level actions close to the template they affect.

### 9.2 Template-library operations

- New.
- Import folder.
- Duplicate.
- Rename.
- Delete with confirmation.
- Export.
- Import template package.
- Enable or disable in Explorer.
- Reorder Explorer entries.
- Refresh or repair Explorer registration.

### 9.3 Structure-tree operations

- Add folder.
- Add text file.
- Attach an existing file.
- Rename inline.
- Move through drag-and-drop.
- Cut, copy, paste, and duplicate nodes.
- Delete with undo while the editor remains open.
- Expand and collapse hierarchy.
- Display validation errors on the affected node.

### 9.4 File editing

Text files should have an integrated plain-text editor with:

- Monospaced text.
- Variable insertion.
- Encoding selection.
- Line-ending selection.
- A resolved preview.

Loading an older file into editable text content may be considered later; it is not required for the initial release.

`Add text file...` creates a token-aware file with empty content and immediately enters inline rename mode; it does not force a `.txt` extension. `Attach existing file...` copies a selected file verbatim and is the path for binary or intentionally immutable content.

Syntax highlighting is optional and not required for the initial release.

### 9.5 Unsaved changes

The editor tracks dirty state and must not silently discard changes when switching templates, refreshing, closing, importing, or deleting. Save failures leave the in-memory document intact.

### 9.6 Validation and preview

The editor provides:

- Continuous structural validation.
- A list of warnings and errors.
- A single non-mutating `Test template...` preview that shows resolved names, paths, contents, and collision warnings using a configurable sample folder.
- Clear distinction between blocking errors and advisory warnings.

The editor follows the current Windows appearance by default. Settings may override this with explicit Light or Dark modes. All three modes are initial-release requirements.

## 10. Template model

### 10.1 General requirements

A template is a versioned JSON document with:

- Schema version.
- Stable template ID.
- Display name and optional description.
- Template revision number.
- A recursive tree of folder and file nodes.
- Default collision behavior.
- Optional template-specific variables in the future.

Explorer enabled state and menu order are local library settings, not template properties. They do not travel with exported templates.

### 10.2 Version 1 contract

The proposed exact version 1 contract is documented in [`JSON-CONTRACT.md`](JSON-CONTRACT.md) and enforced structurally by [`schemas/template-v1.schema.json`](schemas/template-v1.schema.json). A complete valid example is [`examples/3ds-max-template.json`](examples/3ds-max-template.json).

The persisted top-level shape is:

```json
{
  "schemaVersion": 1,
  "templateId": "01993a6c-f686-7e81-b530-35940e1b4072",
  "name": "3ds Max Project",
  "description": "Standard 3ds Max production structure",
  "templateVersion": 1,
  "createdAtUtc": "2026-08-24T12:30:00.000Z",
  "modifiedAtUtc": "2026-08-24T12:30:00.000Z",
  "defaultFileCollision": "skip",
  "nodes": [
    {
      "nodeId": "01993a6d-0001-7a10-8a01-000000000001",
      "type": "folder",
      "name": "SCENES",
      "children": []
    },
    {
      "nodeId": "01993a6d-0002-7a10-8a01-000000000002",
      "type": "folder",
      "name": "RENDERS",
      "children": []
    },
    {
      "nodeId": "01993a6d-0004-7a10-8a01-000000000004",
      "type": "textFile",
      "name": "{{project_name}}.mxp",
      "encoding": "utf-8",
      "lineEndings": "crlf",
      "content": "[Directories]\nProjectFolder={{project_path}}\nScenes=.\\SCENES\nRenderOutput=.\\RENDERS\n"
    }
  ]
}
```

### 10.3 Node types

**Confirmed initial node types:**

| Node type | Purpose |
| --- | --- |
| `folder` | Creates a directory and contains child nodes. |
| `textFile` | Creates a text file from inline templated content. |
| `assetFile` | Copies an attached source file into the project. |

Arbitrary executable hooks are deliberately excluded.

The engine and editor use only generic node concepts. They contain no MXP-specific model, parser, tab, or generator. MXP, Maya `workspace.mel`, Unreal `.uproject`, Godot `project.godot`, production notes, manifests, and similar formats are represented as ordinary editable text files. Binary DCC starter scenes and other immutable resources are represented as attached asset files.

### 10.4 Bundled starter templates

The initial installation should demonstrate the generic model with:

- A polished `3ds Max Project` template containing the agreed production folders and an editable `{{project_name}}.mxp` text file.
- A `Photogrammetry Project` template covering capture, processing, exports, references, logs, and delivery.
- A `Generic 3D Production` template that is not tied to a specific DCC application.

Bundled templates are ordinary version 1 templates. Users may inspect, duplicate, edit, export, unregister, or delete their user-owned copies. Application upgrades never overwrite modified user copies.

### 10.5 Template packaging

A managed template is stored as `%LOCALAPPDATA%\ProjectScaffolder\Templates\<templateId>\template.json` with an optional sibling `assets` directory.

Asset references include a relative path and SHA-256 digest. Export and import will package `template.json` and `assets` into a single archive with a product-specific extension that remains an open decision.

### 10.6 Template versioning

- `schemaVersion` describes the application-level JSON contract.
- `templateVersion` describes the user's revision of a specific template.
- Template and structure nodes use stable UUIDv7 identities.
- Template IDs remain unchanged when a template is renamed or edited.
- Duplicating a template creates a new template ID.
- Importing the same exported template requires an explicit choice between replacing/updating it and importing it as a new template.

`templateVersion` increments when a saved change affects generated output or project provenance. Description, display order, and Explorer enablement changes do not increment it. Exact rules are in [`JSON-CONTRACT.md`](JSON-CONTRACT.md).

## 11. Variables

### 11.1 Version 1 built-in variables

| Variable | Meaning | Example |
| --- | --- | --- |
| `{{project_name}}` | Name of the target folder. | `Hotel Lobby` |
| `{{project_path}}` | Absolute target-folder path. | `D:\Projects\Hotel Lobby` |
| `{{created_date}}` | Creation date in ISO format. | `2026-08-24` |
| `{{created_at_utc}}` | Full UTC creation timestamp. | `2026-08-24T12:41:37.284Z` |
| `{{project_id}}` | UUIDv7 assigned to the project. | `01993a74-7a89-7c13-9c54-76b8bf2bb785` |
| `{{user_name}}` | Current Windows username. | `Zdravko` |

The contract draft makes variable names case-sensitive and requires the lowercase canonical spellings above. Unknown or malformed variable expressions are blocking errors.

### 11.2 Variable evaluation

- One generation context is created before writing any output.
- `project_id` and timestamps are generated once and reused everywhere.
- Variables may be used in folder names, filenames, and text content.
- Variables may not be used to bypass path-containment validation.
- Unknown variables are blocking validation errors by default.
- Attached binary assets do not receive content substitution.

### 11.3 Future variables

User-defined variables that require prompting conflict with the confirmed no-popup workflow. They are deferred unless they can obtain values from noninteractive defaults, environment data, or template configuration.

## 12. `.projectID.json` contract

### 12.1 Purpose

`.projectID.json` gives every scaffolded project a durable identity that survives folder renames and moves and can later serve as an external key in a project database.

### 12.2 Canonical filename

The exact canonical filename is:

```text
.projectID.json
```

Windows normally treats filenames case-insensitively, but application code and future scanners should write and display the canonical spelling exactly.

### 12.3 Schema version 1

The exact structural contract is [`schemas/project-id-v1.schema.json`](schemas/project-id-v1.schema.json), its semantic rules are in [`JSON-CONTRACT.md`](JSON-CONTRACT.md), and [`examples/projectID.json`](examples/projectID.json) is a valid test vector.

```json
{
  "schemaVersion": 1,
  "projectId": "01993a74-7a89-7c13-9c54-76b8bf2bb785",
  "createdAtUtc": "2026-08-24T12:41:37.284Z",
  "projectName": "Hotel Lobby",
  "template": {
    "id": "01993a6c-f686-7e81-b530-35940e1b4072",
    "name": "3ds Max Project",
    "version": 3
  },
  "generator": {
    "name": "Project Scaffolder",
    "version": "2.0.0"
  }
}
```

### 12.4 Field semantics

| Field | Contract |
| --- | --- |
| `schemaVersion` | Version of the metadata contract, initially `1`. |
| `projectId` | UUIDv7 generated once for the project. |
| `createdAtUtc` | ISO 8601 UTC timestamp created in the same generation context as the ID. |
| `projectName` | Target folder name at creation time; informative and not authoritative after renaming. |
| `template.id` | Stable ID of the template used to create the project. |
| `template.name` | Template display name at creation time. |
| `template.version` | Output-affecting template revision at creation time. |
| `generator.name` | Product name that generated the metadata. |
| `generator.version` | Application version that generated the metadata. |

The absolute project path must not be stored because it becomes stale when a project is moved.

### 12.5 Generation behavior

- Generate the project ID and timestamp during preflight.
- If valid metadata already exists, reuse its project ID and original creation timestamp in the generation context.
- Allow `{{project_id}}` and `{{created_at_utc}}` in other generated files.
- Write `.projectID.json` only after all other required generation steps succeed.
- Write it atomically using a temporary file in the target directory followed by a rename.
- Never overwrite a valid existing `.projectID.json` automatically.
- When valid metadata already exists and its template ID matches the selected template, treat the folder as the same project and preserve its identity and original creation timestamp.
- When valid metadata already exists and its template ID differs from the selected template, stop during preflight without changing the project.
- An invalid or unreadable existing metadata file is a blocking error unless the user repairs it through an explicit tool.
- The metadata file is engine-owned and does not appear as an editable template node.
- Apply the Windows Hidden attribute after creating the metadata file.

### 12.6 Copies, backups, and derived projects

Copying a project also copies its ID. This is correct for backups and relocations but incorrect when a copy is intended to become a new project.

A future explicit `Assign New Project ID` operation may generate a new ID while retaining lineage:

```json
{
  "projectId": "new UUIDv7",
  "derivedFromProjectId": "previous UUID"
}
```

Automatic duplicate detection requires a database or index and is deferred.

### 12.7 Visibility

The leading dot does not hide a file on Windows, so the runner applies the Windows Hidden attribute to `.projectID.json` after creating it. The file remains available to software, source control, backup tools, and users who enable hidden-file display. If applying the attribute fails, project generation reports a failure and follows normal rollback rules so the metadata contract is not only partially fulfilled.

## 13. Generation engine

### 13.1 Generation stages

1. Resolve target folder and template.
2. Load and validate the template schema.
3. Inspect existing `.projectID.json` if present and verify that its template ID matches the selected template.
4. Create one immutable generation context.
5. Resolve variables.
6. Normalize and validate all output paths.
7. Detect duplicate and conflicting planned outputs.
8. Determine collision actions without writing.
9. Create directories.
10. Create or copy files according to collision rules.
11. Write `.projectID.json` when needed.
12. Flush the operation log and exit successfully.

### 13.2 Idempotence

Running the same template again should be safe:

- Existing directories are reused.
- Existing files follow their configured collision behavior.
- Existing valid project metadata is preserved only when its template ID matches the selected template.
- A second run does not generate a second project ID.
- The runner performs no work when every planned item already exists and all policies say `skip`.

### 13.3 Collision policies

**Initial policies for generated files:**

| Policy | Behavior |
| --- | --- |
| `skip` | Preserve the existing file and continue. Recommended default. |
| `overwrite` | Atomically replace the existing file. Must be explicitly selected per template or file. |
| `fail` | Stop before generation if the file exists. |

Directories always use a merge/reuse policy unless a file already occupies the required directory path, which is a blocking conflict.

### 13.4 Rollback

The engine records an in-memory operation journal of directories and files created during the current invocation.

On failure:

- Delete newly created files.
- Delete newly created directories only when they are empty.
- Never delete a pre-existing artifact.
- Never attempt to restore an overwritten file unless an atomic backup was successfully created first.
- Log incomplete rollback and tell the user which paths require inspection.

Because the target root already exists, complete transactional generation is not always possible. Preflight validation is therefore the primary defense against partial output.

## 14. Path and filesystem safety

### 14.1 Required validation

Every resolved output path must:

- Be relative to the target root before combination.
- Resolve inside the target root after canonicalization.
- Reject `..` traversal that escapes the root.
- Reject rooted paths, UNC paths, drive-qualified paths, and device paths.
- Reject invalid Windows filename characters.
- Reject alternate data stream syntax.
- Reject reserved Windows device names such as `CON`, `NUL`, `PRN`, `AUX`, `COM1`, and `LPT1`.
- Reject path components ending in spaces or periods.
- Respect practical Windows path-length constraints supported by the application manifest and filesystem.
- Detect duplicates using case-insensitive Windows path comparison.
- Detect file-versus-directory conflicts.

### 14.2 Reparse points and symbolic links

An existing directory inside the target may be a junction, mount point, or symbolic link that redirects writes outside the apparent project root.

**Proposed safe default:** Refuse to traverse an existing reparse point during generation unless a future advanced setting explicitly permits it. The error must identify the affected path.

### 14.3 Untrusted templates

Imported templates are data, not code. The initial release must not:

- Execute scripts stored in a template.
- Expand environment text into commands.
- Load arbitrary DLLs.
- Follow asset paths outside the managed template package during generation.

## 15. Proposed application architecture

### 15.1 Technology

- C#.
- .NET 10 LTS.
- WPF for the editor.
- `System.Text.Json` for persistent JSON contracts.
- Self-contained x64 publishing initially.
- Native C++ only for a future modern Windows 11 shell extension, if implemented.

### 15.2 Logical components

```text
ProjectScaffolder.sln
├─ ProjectScaffolder.Core
│  ├─ Template domain model
│  ├─ Validation
│  ├─ Variable resolution
│  ├─ Generation planning
│  ├─ Generation engine
│  └─ Project metadata contract
├─ ProjectScaffolder.Editor
│  ├─ WPF UI
│  ├─ Template library
│  ├─ Tree editing
│  ├─ Preview
│  └─ Settings
├─ ProjectScaffolder.Runner
│  ├─ Explorer invocation parsing
│  ├─ Silent generation host
│  └─ Failure reporting
├─ ProjectScaffolder.Windows
│  ├─ Classic menu registration
│  ├─ Windows path behavior
│  ├─ File attributes
│  └─ Installation health checks
├─ ProjectScaffolder.Core.Tests
└─ ProjectScaffolder.Windows.Tests
```

Project names and boundaries may change during baseline review. The required principle is that the editor, runner, registry integration, and core generation logic are not tightly coupled.

### 15.3 Dependency direction

```text
Editor ───────┐
              ├──> Core
Runner ───────┘

Editor ───────┐
              ├──> Windows integration
Installer ────┘

Windows integration ──> Core abstractions only where necessary
```

Core must not depend on WPF, WinForms, Registry APIs, or installer technology. This allows most logic to be tested without Explorer or Registry access.

### 15.4 Process model

- The editor is a normal single-instance desktop process unless multi-window editing is later justified.
- The runner is a short-lived, consoleless process.
- The runner accepts a template ID and target path through explicit arguments.
- The runner returns meaningful process exit codes for diagnostics even though Explorer does not display them.
- Registry commands invoke executables directly, never through PowerShell or `cmd.exe`.

The version 1 runner command contract is:

```text
ProjectScaffolder.Runner.exe --template-id <canonical UUIDv7> --target <existing folder>
```

`--templates-root` and `--logs-root` are diagnostic/test overrides. `--no-ui` suppresses the failure dialog for automated testing. Exit codes are stable for automation and diagnostics: `0` success, `2` invalid arguments, `3` missing template, `4` invalid template, `5` target conflict, `6` generation failure, `7` incomplete rollback, and `10` unexpected failure.

## 16. Storage

### 16.1 Confirmed locations

| Data | Location |
| --- | --- |
| Installed binaries | `%LOCALAPPDATA%\Programs\ProjectScaffolder` |
| User templates | `%LOCALAPPDATA%\ProjectScaffolder\Templates` |
| Application settings | `%LOCALAPPDATA%\ProjectScaffolder\Settings` |
| Removed template recovery | `%LOCALAPPDATA%\ProjectScaffolder\Trash` |
| Logs | `%LOCALAPPDATA%\ProjectScaffolder\Logs` |
| Disposable caches | `%LOCALAPPDATA%\ProjectScaffolder\Cache` |
| Exported template packages | User-selected location |

Templates use non-roaming Local AppData because attached binary assets may be large and are specific to the current workstation. Application binaries and user-owned data remain in separate directory trees so updates cannot replace templates or settings.

Explorer enablement and template order are stored in `%LOCALAPPDATA%\ProjectScaffolder\Settings\template-library.json` using [`schemas/template-library-v1.schema.json`](schemas/template-library-v1.schema.json). This local state is not included in exported templates.

### 16.2 Persistence rules

- Save JSON atomically through a sibling temporary file and replace/rename.
- Keep one recoverable backup of a template before replacing it.
- Never overwrite an existing user template while seeding bundled examples.
- Preserve unknown future JSON properties only if the chosen serialization strategy supports safe round-tripping; otherwise refuse to save a newer unsupported schema.
- Corrupt templates remain discoverable in the editor with an error state rather than disappearing silently.

## 17. Installation and uninstallation

### 17.1 Installer requirements

- Provide a normal per-user installation executable.
- Install without elevation under `%LOCALAPPDATA%\Programs\ProjectScaffolder`.
- Install self-contained application binaries.
- Create Start menu shortcuts.
- Register the classic Explorer integration.
- Seed default templates without replacing user content.
- Support repair and upgrade.
- Record an installed product version.
- Avoid requiring PowerShell scripts during normal end-user installation.

Installer technology is open and should be chosen after a packaging spike. Candidates may include WiX-based MSI/bundle packaging or another reputable Windows installer system.

### 17.2 Upgrade requirements

- Preserve templates and settings.
- Migrate supported older schemas explicitly.
- Refresh application-owned Registry entries.
- Replace binaries safely even if the editor was recently used.
- Provide a clear failure and rollback path.

### 17.3 Uninstallation

- Remove binaries, shortcuts, and all application-owned Explorer keys.
- Preserve user templates and project files by default.
- Offer explicit removal of application settings and template data if the installer technology supports a clear choice.
- Never search for or remove `.projectID.json` from user projects.

## 18. Diagnostics and error experience

### 18.1 Success behavior

- No dialog.
- No console window.
- No notification by default.
- Optional logging at an appropriate low verbosity.

### 18.2 Failure behavior

A failure dialog should state:

- Which template was selected.
- Which target folder was used.
- A concise explanation.
- Whether rollback completed.
- Where to find the diagnostic log.

Do not show raw stack traces in the primary dialog.

### 18.3 Logs

Logs should include:

- Application version.
- Template ID and version.
- Target path.
- Operation outcome.
- Created, skipped, overwritten, and failed counts.
- Detailed exception information on failure.

Logs must not contain file contents or unnecessary personal information. The runner writes one structured JSON log for every successful or failed invocation and retains at most the newest 100 files and files no older than 30 days. Retention cleanup is best-effort and never converts successful generation into failure.

## 19. Testing strategy

### 19.1 Unit tests

- Template JSON serialization and migration.
- UUIDv7 metadata generation.
- Variable resolution.
- Unknown-variable detection.
- Windows reserved-name validation.
- Root containment and traversal rejection.
- Case-insensitive duplicate detection.
- File-versus-folder conflicts.
- Collision-policy planning.
- Existing metadata preservation.
- Metadata schema serialization.

### 19.2 Filesystem integration tests

Use isolated temporary directories to verify:

- Nested folder and file generation.
- Idempotent repeated execution.
- Text encoding and line endings.
- Asset copying.
- Atomic file replacement.
- Rollback after injected failure.
- Existing user content is never deleted.
- Reparse-point behavior on supported test systems.

### 19.3 Windows integration tests

- Registration and unregistration under isolated test keys where possible.
- Correct command quoting for spaces and special characters.
- Folder-background target resolution.
- Selected-folder target resolution.
- Registration repair.
- Explorer refresh behavior.

### 19.4 UI tests and manual acceptance

- Create, edit, duplicate, import, export, and delete templates.
- Drag and drop nested nodes.
- Unsaved-change prompts.
- Validation navigation.
- Preview accuracy.
- Keyboard navigation and high-DPI behavior.
- Light and dark Windows themes where supported by the chosen UI design.

### 19.5 Installer tests

- Clean installation.
- Upgrade over an older version.
- Repair.
- Uninstall with templates preserved.
- Install paths containing spaces.
- Operation under a standard non-administrator account.

## 20. Performance expectations

- The classic submenu should appear without a perceptible application launch.
- The runner should start quickly and avoid loading the WPF editor assembly.
- Template preflight should be effectively immediate for ordinary project structures.
- Large asset copies may take time, but the no-popup requirement means the application must not display progress unless a future threshold or setting is introduced.
- Explorer integration must never parse large template assets while building a menu.

## 21. Accessibility and localization

### 21.1 Proposed baseline

- Full keyboard access in the editor.
- Visible focus states.
- Screen-reader labels for primary controls.
- High-DPI support.
- Do not communicate validation state through color alone.
- Store user-visible strings so future localization is possible, even if the first release is English-only.

## 22. Implementation milestones and checklist

The version 1.0 design baseline is approved. Implementation may proceed milestone by milestone, with this document updated as remaining decisions are resolved.

### Milestone 0 — Design baseline

- [x] Inspect the original Project Scaffolder.
- [x] Confirm direct in-place generation workflow.
- [x] Confirm classic Explorer menu as the primary integration.
- [x] Confirm `.projectID.json` and UUIDv7 direction.
- [x] Confirm C#/.NET direction.
- [x] Review this design document together.
- [x] Resolve baseline-blocking open questions.
- [x] Mark document version 1.0 as approved.

### Milestone 1 — Foundation and contracts

- [x] Initialize the repository and solution.
- [x] Add formatting, build, and test conventions.
- [x] Implement template and metadata contracts.
- [x] Implement template persistence and schema validation.
- [x] Implement safe path normalization.
- [x] Implement variable resolution.
- [x] Implement generation planning and collision decisions.
- [x] Add core unit tests.

### Milestone 2 — Generation runner

- [x] Implement filesystem generation.
- [x] Implement `.projectID.json` behavior.
- [x] Implement operation journaling and rollback.
- [x] Implement silent success and failure dialog.
- [x] Implement diagnostic logging.
- [x] Add filesystem integration tests.

### Milestone 3 — Template editor

- [x] Establish the WPF visual design.
- [x] Implement template library.
- [x] Implement structure tree and properties panel.
- [x] Implement text-file editor.
- [x] Implement validation and resolved preview.
- [x] Implement unsaved-change protection.

Current verified editor slice:

- [x] Launch a consoleless WPF editor using the approved three-pane layout.
- [x] Follow the Windows application theme and provide runtime System, Light, and Dark choices.
- [x] Load templates from Local AppData, seed the bundled example once, and keep damaged templates visible.
- [x] Create and duplicate templates; add, rename, remove, and reorder basic folder/text nodes.
- [x] Treat the visible project-root row as a selectable insertion target for first-level folders and files.
- [x] Support inline rename through `F2`, a toolbar action, and Explorer-style delayed second-click on an already-selected name.
- [x] Support guarded drag-and-drop with explicit folder/root target highlighting, reliable folder nesting, and returning nodes to the project root.
- [x] Clear node selection when empty tree space is clicked so subsequent additions target the project root.
- [x] Keep the insertion target selected after creation, preventing repeated additions from nesting recursively.
- [x] Automatically maintain recursive folders-first alphabetical ordering after creation, rename, and drag/drop; manual ordering controls are unnecessary.
- [x] Edit text content, encoding, line endings, and collision behavior in the properties pane.
- [x] Continuously validate both contract rules and resolved output-path conflicts.
- [x] Save valid templates atomically with dirty-state tracking and `Ctrl+S`.
- [x] Preserve independent dirty sessions while switching templates and protect all dirty sessions on close.
- [x] Remove templates with confirmation and retain saved packages in the recoverable application Trash folder.
- [x] Add attached-file management and asset-safe duplication.
- [x] Add variable insertion for inline names and editable text, plus a non-mutating resolved preview.

Deferred beyond the initial release: clipboard operations, delete undo, manual library ordering, folder import, template package import/export, text search/replace, and disposable test generation. These features should return only in response to a demonstrated workflow need.

### Milestone 4 — Classic Explorer integration

- [x] Implement the stable `New Project` cascade.
- [x] Implement background-folder and selected-folder targets.
- [x] Implement enabled-template submenu generation.
- [x] Implement separator and Editor command.
- [x] Implement registration health check and automatic startup repair.
- [x] Verify safe quoting and paths containing spaces.

### Milestone 5 — Installer

- [ ] Complete installer technology spike.
- [ ] Build per-user installer executable.
- [ ] Add Start menu and uninstall entries.
- [ ] Seed default templates safely.
- [ ] Test install, upgrade, repair, and uninstall.

### Milestone 6 — Hardening and release

- [ ] Complete security and failure-path review.
- [ ] Complete accessibility and high-DPI review.
- [ ] Test on supported Windows versions.
- [ ] Write end-user `README.md`.
- [ ] Produce signed or clearly identified test release artifacts.
- [ ] Approve version 2.0 release checklist.

### Deferred milestones

- [ ] Native Windows 11 compact-menu integration.
- [ ] Project database/index.
- [ ] Duplicate-ID detection.
- [ ] `Assign New Project ID` and lineage.
- [ ] Optional template update comparison for existing projects.

## 23. Open questions

Questions marked **Baseline** should be answered before implementation begins. Others may be resolved during the named milestone.

| ID | Question | Recommendation | When |
| --- | --- | --- | --- |
| Q-010 | What package extension should exported templates use? | Choose a product-specific ZIP-based extension after naming is final. | Milestone 3 |
| Q-011 | Should Explorer submenu order be manually configurable? | Deferred; initial release uses stable library order. | Post-1.0 |
| Q-013 | Which installer technology best satisfies per-user EXE install, upgrade, and repair? | Decide through a small packaging spike. | Milestone 5 |
| Q-015 | Should imported folders include hidden/system files by default? | Exclude by default and offer an explicit option. | Milestone 3 |

## 24. Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Registry cascade behavior varies across Windows configurations. | Menu may be missing or stale. | Keep integration isolated, add health/repair tools, test supported versions. |
| Templates can resolve unsafe paths. | Data may be written outside the project. | Strict containment, reserved-name, reparse-point, and duplicate validation before writing. |
| In-place generation can fail midway. | Partial project structure. | Comprehensive preflight plus operation journal and conservative rollback. |
| Overwrite policies can destroy user content. | Data loss. | Default to skip; require explicit overwrite; use atomic replacement/backup where possible. |
| Copied projects duplicate IDs. | Future database identity collision. | Document semantics and later provide explicit re-identification with lineage. |
| Binary assets make templates large and less portable. | Slow storage/export and roaming problems. | Managed asset directories, portable archive format, storage policy review. |
| Editor scope grows into a general IDE. | Delayed delivery and complexity. | Limit initial editor to structure, plain text, variables, validation, and preview. |
| Installer and signing choices constrain modern-menu support. | Rework for future Windows 11 integration. | Isolate shell provider and revisit packaging before modern-menu milestone. |

## 25. Future database compatibility

No database will be implemented initially. The filesystem remains authoritative for project identity through `.projectID.json`.

A future indexer should be able to:

- Scan configured roots for `.projectID.json`.
- Use `projectId` as a unique external key.
- Record the current path independently of the metadata file.
- Detect multiple paths with the same ID.
- Treat template ID and version as provenance rather than current project state.
- Store mutable business data outside `.projectID.json`.

The metadata schema should remain small. Database-specific fields such as client, status, assignee, or billing code should not be added without a separate design decision.

## 26. Decision log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-08-24 | Rebuild as Project Scaffolder 2.0 rather than directly extending the original code. | The original proves the workflow but tightly couples its UI, template model, MXP specialization, and registry behavior. |
| 2026-08-24 | Preserve direct generation inside the current folder. | This is an established, efficient workflow: create, name, enter, and scaffold the folder. |
| 2026-08-24 | Do not prompt or display success messages during normal generation. | Explorer invocation should remain immediate and unobtrusive. |
| 2026-08-24 | Use one classic `New Project` cascade with enabled templates and Editor. | The primary user has intentionally restored the classic Windows menu and prefers it. |
| 2026-08-24 | Defer the modern Windows 11 compact menu while preserving an extension path. | Modern integration adds native COM and packaging complexity that is not required for the primary workflow. |
| 2026-08-24 | Use C#/.NET with WPF proposed for the editor. | It offers strong Windows integration, maintainability, tree editing, data binding, and self-contained distribution. |
| 2026-08-24 | Represent MXP as a generic templated text file. | The core model should support many project types without specialized generator code. |
| 2026-08-24 | Generate `.projectID.json` with UUIDv7 and UTC creation time. | The project needs a stable, database-ready identity with chronological ordering and explicit readable provenance. |
| 2026-08-24 | Support Windows 11 x64 only. | Windows 10 compatibility is not a product requirement and should not add design or testing overhead. |
| 2026-08-24 | Keep the product name `Project Scaffolder`. | It is clear, accurately describes the tool, and preserves continuity with the original application. |
| 2026-08-24 | Require `.projectID.json` for every scaffolded project and hide it with the Windows Hidden attribute. | Consistent identity supports future indexing while keeping normal project roots visually clean. |
| 2026-08-24 | Permit template reruns only when the existing metadata names the same template ID. | Idempotent repair is useful, but applying an unrelated structure to an identified project is unsafe without an explicit workflow. |
| 2026-08-24 | Skip existing files by default and reuse existing folders. | This preserves idempotence and protects user-owned changes during repeated generation. |
| 2026-08-24 | Support folder, templated-text-file, and attached-asset nodes initially. | An empty text file already covers zero-length output, while attached assets cover binary content; a fourth empty-file concept would duplicate editor and engine behavior. |
| 2026-08-24 | Install per user under `%LOCALAPPDATA%\Programs` and keep templates/settings in non-roaming `%LOCALAPPDATA%`. | This matches the per-user Explorer registration model, avoids UAC, simplifies updates, and prevents large attached assets from roaming with a Windows profile. |
| 2026-08-24 | Approve design baseline version 1.0 for implementation. | The product workflow, platform, identity contract, safety defaults, storage model, and initial scope have been reviewed and accepted. |
| 2026-08-24 | Approve JSON contract version 1 and design revision 1.1. | Strict schemas, stable identities, separated local Explorer state, deterministic variables, and explicit file behavior provide a testable implementation contract. |
| 2026-08-24 | Position Project Scaffolder as artist-first but keep its engine and editor generic. | The primary user receives excellent 3ds Max and photogrammetry workflows while the same folder, text, asset, and variable model remains useful across other DCC and non-art projects. |
| 2026-08-24 | Confirm the three-pane editor concept and refine template-level controls. | Explorer registration belongs on each template row, saving belongs in the template pane, file creation should be concise, and testing should use one clearly named workflow. |
| 2026-08-24 | Follow the Windows theme by default with Light and Dark overrides. | The editor should feel native on the user's system while retaining an explicit accessibility and preference choice. |
| 2026-08-24 | Complete the Milestone 1 Core foundation on .NET 10. | Strict runtime contracts, semantic validation, deterministic variables, contained output planning, asset verification, collision preflight, encoding, and atomic persistence establish a testable safety boundary before filesystem generation or UI work. |
| 2026-08-24 | Complete the Milestone 2 generation runner and retain bounded logs for all invocations. | The consoleless runner now applies validated plans transactionally, creates stable hidden project identity, rolls back conservatively, stays silent on success, reports failures clearly, and preserves enough structured diagnostics without unbounded storage growth. |
| 2026-08-24 | Confirm WPF and an editor-local mutable view-model layer for the template editor. | WPF implements the approved Windows-native three-pane experience, while converting to strict immutable Core contracts only for validation and persistence keeps UI state out of the generation engine. |
| 2026-08-24 | Make template removal recoverable through an application Trash folder. | Templates may contain valuable attached assets; moving the complete package out of the active library provides immediate removal without making an accidental confirmation permanently destructive. |
| 2026-08-24 | Stage newly attached and replacement files outside saved template packages until Save succeeds. | Content-addressed managed paths preserve the last saved asset reference during editing, avoid abandoned partial packages, permit safe replacement cleanup, and let duplication copy assets independently. |
| 2026-08-24 | Make `Test template...` a non-mutating resolved preview with an editable sample target path. | Users can verify variable values, output paths, text contents, attachment integrity, and collision outcomes without writing a project. |
| 2026-08-24 | Keep preview non-mutating and make disposable generation a separate explicit action using the production generator. | This tests the actual filesystem behavior, including unsaved edits and staged assets, while keeping ordinary preview safe; generated workspaces can be opened and are deleted only on explicit request. |
| 2026-08-24 | Refocus the initial release on defining structures, including files, registering templates, and generating silently from Explorer; defer editor conveniences and disposable generation. | Project Scaffolder is a focused scaffolding utility, not a miniature IDE. This decision supersedes the disposable-generation decision above and moves active work to Explorer integration. |
| 2026-08-24 | Implement the classic menu as two per-user static cascades maintained by the editor. | Separate background (`%V`) and selected-folder (`%1`) command sets preserve the direct in-place workflow without elevation, COM code, or generation inside Explorer. |

## 27. Authoritative references

- UUID specification, including UUIDv7: <https://www.rfc-editor.org/rfc/rfc9562.html>
- .NET UUIDv7 API: <https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7?view=net-10.0>
- .NET support policy: <https://dotnet.microsoft.com/en-us/platform/support/policy>
- WPF desktop documentation: <https://learn.microsoft.com/en-us/dotnet/desktop/wpf/>
- Microsoft classic cascading-menu guidance: <https://learn.microsoft.com/en-us/windows/win32/shell/how-to-create-cascading-menus-with-the-extendedsubcommandskey-registry-entry>
- Microsoft packaged Explorer-command guidance: <https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer>
- JSON Schema Draft 2020-12: <https://json-schema.org/draft/2020-12>

## 28. Approval

Design baseline version 1.0 was approved on 2026-08-24. Future changes remain permitted, but changes to confirmed product behavior or persistent data contracts must update this document and add an entry to the decision log.

Approval authorizes implementation according to the milestone plan. It does not convert proposed or deferred items into confirmed requirements.
