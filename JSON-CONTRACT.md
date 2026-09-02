# Project Scaffolder — JSON Contracts

| Field | Value |
| --- | --- |
| Status | Approved |
| Contract versions | Template 1–2; identity, structure, and library 1 |
| Last updated | 2026-09-02 |
| JSON Schema dialect | Draft 2020-12 |

## 1. Scope and authority

This document defines the semantic rules for Project Scaffolder's persistent JSON documents. The machine-readable schemas define their structural rules:

- [`schemas/template-v1.schema.json`](schemas/template-v1.schema.json)
- [`schemas/template-v2.schema.json`](schemas/template-v2.schema.json)
- [`schemas/project-id-v1.schema.json`](schemas/project-id-v1.schema.json)
- [`schemas/project-structure-v1.schema.json`](schemas/project-structure-v1.schema.json)
- [`schemas/template-library-v1.schema.json`](schemas/template-library-v1.schema.json)

The schemas and this document are jointly normative. JSON Schema validates shape, required properties, primitive constraints, and discriminated node types. This document defines cross-field and filesystem rules that JSON Schema cannot express completely.

Each contract version is strict. Unknown properties are rejected so misspellings cannot silently change generation behavior. Template version 2 adds semantic folder roles; it does not change project identity version 1.

## 2. JSON serialization rules

- Documents are encoded as UTF-8 without a byte-order mark.
- Application-written JSON is indented with two spaces.
- Property order is cosmetic and must not affect behavior.
- Object property names are case-sensitive.
- Enum values and UUID text are lowercase and case-sensitive.
- UUIDs use canonical hyphenated lowercase text.
- UTC timestamps use ISO 8601/RFC 3339 text ending in `Z`.
- Writers emit the canonical property names shown by the schemas.
- Readers reject unsupported `schemaVersion` values without modifying the document.
- Saves use a sibling temporary file followed by atomic replace or rename.

## 3. Managed template layout

Each editable template has a directory named with its template ID:

```text
%LOCALAPPDATA%\ProjectScaffolder\Templates\
└─ 01993a6c-f686-7e81-b530-35940e1b4072\
   ├─ template.json
   └─ assets\
      └─ optional attached files
```

Rules:

- The directory name must exactly match `templateId`.
- `template.json` is required.
- `assets` is optional when the template has no `assetFile` nodes.
- Asset paths stored in JSON always use `/`, regardless of Windows path separators.
- Assets must remain inside the template's `assets` directory.
- The application must not resolve symbolic links or reparse points from an asset to a file outside the managed template directory.
- Temporary files and recoverable backups use reserved application-controlled names and are not part of an exported template.

## 4. Template document

### 4.1 Top-level properties

| Property | Required | Meaning |
| --- | --- | --- |
| `schemaVersion` | Yes | Structural contract version. Supported values are `1` and `2`; the editor writes `2` when saving. |
| `templateId` | Yes | Stable UUIDv7 identity of this template. |
| `name` | Yes | User-facing template name, 1–120 characters. |
| `description` | No | Optional user-facing description, at most 2,000 characters. |
| `templateVersion` | Yes | Positive revision number for output-affecting changes. |
| `createdAtUtc` | Yes | UTC timestamp when the template identity was created. |
| `modifiedAtUtc` | Yes | UTC timestamp of its last saved content change. |
| `defaultFileCollision` | Yes | Default behavior for existing generated files. |
| `nodes` | Yes | Ordered top-level structure nodes created inside the existing target folder. |

The target project root is implicit. It is never represented by a persisted folder node. The editor may display a virtual root using the sample or current project name.

Explorer enablement and menu order are deliberately absent. They belong to the local template-library document and do not travel when a template is exported.

### 4.2 Template identity and revision

- Create `templateId` as UUIDv7 when creating or importing a template as new.
- Renaming or editing a template never changes `templateId`.
- Duplicating a template creates a new template ID and new node IDs.
- Reordering, adding, deleting, renaming, or changing a node increments `templateVersion`.
- Changing `name` increments `templateVersion` because the name is written as project provenance.
- Changing `defaultFileCollision` increments `templateVersion`.
- Changing only `description` does not increment `templateVersion`.
- Explorer enablement and order never increment `templateVersion`.
- `modifiedAtUtc` changes whenever persisted template content changes; saving an unchanged template does not rewrite it.
- `createdAtUtc` never changes after initial creation.
- `modifiedAtUtc` must not precede `createdAtUtc`.

### 4.3 Node identity

Every node has a stable UUIDv7 `nodeId`.

- Node IDs must be unique across the complete template tree.
- Moving or editing a node preserves its ID.
- Duplicating a node recursively assigns new IDs to the duplicate and all descendants.
- Node IDs are editor/domain identities; they do not appear in generated projects.

### 4.4 Node names

`name` represents exactly one output path component, not a relative path.

- `/` and `\` are forbidden in a node name.
- `.` and `..` are forbidden.
- Windows-invalid characters, device names, trailing spaces, and trailing periods are forbidden.
- Variables may appear in names.
- Validation runs both before and after variable substitution.
- Resolved names are compared case-insensitively for duplicate detection.
- `.projectID.json` and `.projectStructure.json` are reserved at the target root and cannot be created by a template node using any letter casing.

### 4.5 Folder node

```json
{
  "nodeId": "01993a6d-0001-7a10-8a01-000000000001",
  "type": "folder",
  "name": "SCENES",
  "hidden": false,
  "role": "capture.raw",
  "children": []
}
```

- `children` is required and preserves user-defined order.
- `hidden` is optional and defaults to `false` when absent, preserving compatibility with existing version 1 templates.
- When `hidden` is `true`, the generator applies the Windows Hidden attribute only if it creates the folder.
- A reused existing folder is never hidden or unhidden by generation.
- The implicit project root cannot be hidden by a template.
- Existing output directories are reused.
- Folder nodes do not have a collision property.
- A file occupying the required directory path is a blocking conflict.
- Template schema version 2 permits the optional `role` property. Version 1 rejects it.
- A role is a lowercase dotted identifier and may occur on only one folder in a template.
- The known capture roles are `capture.raw`, the destination for original camera captures, and `capture.processed`, the destination for corrected reconstruction inputs.
- Unknown well-formed roles round-trip and are ignored by consumers that do not understand them.
- The editor exposes known roles through a controlled selector; folder names and template names never imply a role.

### 4.6 Text-file node

```json
{
  "nodeId": "01993a6d-0003-7a10-8a01-000000000003",
  "type": "textFile",
  "name": "{{project_name}}.mxp",
  "encoding": "utf-8",
  "lineEndings": "crlf",
  "content": "[Directories]\nProjectFolder={{project_path}}\n"
}
```

Supported version 1 encodings:

- `utf-8`: UTF-8 without a byte-order mark.
- `utf-8-bom`: UTF-8 with a byte-order mark.
- `utf-16-le`: little-endian UTF-16 with a byte-order mark.
- `windows-1252`: Windows code page 1252; generation fails if content cannot be represented without loss.

Supported line endings:

- `crlf`: Windows carriage-return and line-feed.
- `lf`: line-feed only.

Before encoding, the generator normalizes all line endings in `content` to the chosen value. Variable substitution occurs before encoding. A trailing newline exists only when `content` contains one.

`collision` is optional and inherits from `defaultFileCollision` when absent.

### 4.7 Asset-file node

```json
{
  "nodeId": "01993a6d-0004-7a10-8a01-000000000004",
  "type": "assetFile",
  "name": "studio-logo.png",
  "asset": {
    "path": "assets/01993a6d-0004-logo.png",
    "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
  }
}
```

- `asset.path` is relative to the template directory and begins with `assets/`.
- `asset.path` cannot contain an empty segment, `.` segment, `..` segment, drive prefix, UNC prefix, or backslash.
- The referenced file must exist and resolve inside the managed `assets` directory.
- `asset.sha256` is the lowercase SHA-256 digest of the exact stored bytes.
- The editor recomputes the digest when attaching or replacing an asset.
- Import, load, preview, test generation, and real generation verify the digest.
- Asset bytes are copied verbatim; variables are not substituted within them.
- `collision` is optional and inherits from `defaultFileCollision` when absent.

### 4.8 Collision values

| Value | Behavior |
| --- | --- |
| `skip` | Preserve the existing file and continue. This is the required default for new templates. |
| `overwrite` | Atomically replace the existing file. It must be explicitly configured at template or node level. |
| `fail` | Abort during preflight when the output file already exists. |

An explicit node-level `collision` overrides `defaultFileCollision` for that node only.

## 5. Variables

Version 1 has exactly these built-in variables:

| Variable | Value |
| --- | --- |
| `{{project_name}}` | Name of the target directory at generation time. |
| `{{project_path}}` | Canonical absolute target-directory path. |
| `{{project_id}}` | New or existing UUIDv7 project ID. |
| `{{created_at_utc}}` | New or existing project creation timestamp. |
| `{{created_date}}` | UTC calendar date derived from `created_at_utc`, formatted `yyyy-MM-dd`. |
| `{{user_name}}` | Current Windows account name. |

Rules:

- Variable names are case-sensitive and use the lowercase canonical spellings above.
- An unknown or malformed `{{...}}` expression is a blocking error.
- Escaping literal `{{` and `}}` is not supported in version 1.
- Substitution is a single pass; substituted values are never interpreted as more variables.
- Variables are allowed only in node names and text-file content.
- Variables are not allowed in template metadata or asset source paths.
- One immutable generation context supplies all variable values.
- Existing valid `.projectID.json` supplies `project_id`, `created_at_utc`, and therefore `created_date` during an idempotent rerun.
- Resolved node names remain subject to all path and collision validation.

## 6. Template-library document

The local library state is stored at:

```text
%LOCALAPPDATA%\ProjectScaffolder\Settings\template-library.json
```

Example:

```json
{
  "schemaVersion": 1,
  "templateOrder": [
    "01993a6c-f686-7e81-b530-35940e1b4072"
  ],
  "explorerTemplateIds": [
    "01993a6c-f686-7e81-b530-35940e1b4072"
  ]
}
```

- `templateOrder` is an ordered list of template IDs.
- `explorerTemplateIds` is the ordered-independent set of templates enabled in Explorer.
- Duplicate IDs are invalid.
- IDs not found in the current template directory are ignored with a warning and removed on the next successful save.
- Discovered templates missing from `templateOrder` appear after listed templates, ordered by name, until the user reorders or saves the library.
- A newly created or imported template is not enabled in Explorer automatically.
- Exported template packages never include this document.

## 7. Project identity document

The generator owns `.projectID.json`; templates cannot define or edit it.

Its exact structure is defined by [`schemas/project-id-v1.schema.json`](schemas/project-id-v1.schema.json). A valid example is [`examples/projectID.json`](examples/projectID.json).

Additional semantic rules:

- `projectId` and `template.id` are UUIDv7 values.
- `createdAtUtc` ends in `Z` and records the initial scaffold operation.
- `projectName` records the target directory name at creation time and does not change when the directory is later renamed.
- `template.name` and `template.version` are creation-time provenance snapshots.
- `generator.name` is exactly `Project Scaffolder`.
- The document contains no absolute project path.
- Unknown properties are rejected in schema version 1.
- A valid existing document is accepted only when `template.id` matches the invoked template.
- An idempotent rerun does not update any metadata field.
- The generator writes the document atomically after other required outputs succeed, applies the Windows Hidden attribute, then rereads and validates it before reporting success.

## 8. Project structure document

The generator owns `.projectStructure.json`; templates cannot define or edit it as an ordinary output. It is created when a template declares at least one semantic path and is updated on a valid rerun of the same template.

Its exact structure is defined by [`schemas/project-structure-v1.schema.json`](schemas/project-structure-v1.schema.json). A valid example is [`examples/projectStructure.json`](examples/projectStructure.json).

```json
{
  "schemaVersion": 1,
  "projectId": "01993a74-7a89-7c13-9c54-76b8bf2bb785",
  "paths": {
    "capture.raw": "DATASET/raw",
    "capture.processed": "DATASET/processed"
  }
}
```

Rules:

- `projectId` must match the neighboring valid `.projectID.json`.
- Paths are resolved generation outputs relative to the project root and always use `/` in JSON.
- Absolute paths, backslashes, empty segments, `.` segments, and `..` segments are invalid.
- A declaration records intended structure; consumers still verify current filesystem existence and containment.
- An existing invalid, mismatched, or reparse-point structure document blocks generation before output is changed.
- Rerunning the matching template atomically replaces a valid existing structure document with the current resolved roles.
- If a previous structure document exists and the current template declares no roles, it is retained as a valid document with an empty `paths` object rather than left stale.
- The generator applies the Windows Hidden attribute, rereads the document, and validates it before committing generation.
- Structure failure participates in the same conservative rollback transaction as generated files and identity creation.
- The editor's metadata-only update validates the matching identity and current template roles, requires every resolved role directory to exist, and writes no project content other than `.projectStructure.json`.

The document is a durable separate contract. It is not a temporary extension of `.projectID.json`, and Catalog's private database is not part of this contract.

## 9. Validation layers

Validation occurs in this order:

1. JSON syntax.
2. JSON Schema structure.
3. Contract semantics and cross-field checks.
4. Template-package asset containment and hashes.
5. Variable recognition.
6. Resolved Windows name and path safety.
7. Case-insensitive output duplication and file/folder conflicts.
8. Existing target collisions and project identity compatibility.

The editor may save only a valid contract. Corrupt or unsupported templates remain visible in the library as read-only error entries so they do not disappear silently.

## 10. Examples

- [`examples/3ds-max-template.json`](examples/3ds-max-template.json)
- [`examples/photogrammetry-template-v2.json`](examples/photogrammetry-template-v2.json)
- [`examples/projectID.json`](examples/projectID.json)
- [`examples/projectStructure.json`](examples/projectStructure.json)
- [`examples/template-library.json`](examples/template-library.json)

Examples are test vectors. Once the contract is approved, automated contract tests must validate them on every build.

## 11. Forward compatibility

- New optional behavior requires a new schema version when version 1 readers would otherwise misinterpret it.
- Readers reject unsupported schema versions and never rewrite them.
- Migrations create a recoverable backup before writing the upgraded document.
- A migration preserves template and node identities unless the source lacks them.
- A schema-version change is distinct from `templateVersion`.
- Future project lineage fields require a later `.projectID.json` schema version.
