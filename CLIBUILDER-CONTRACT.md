# Project Scaffolder — CLIBuilder Integration Contract

## Status and ownership

This document defines how CLIBuilder consumes semantic project paths written by Project Scaffolder. Project Scaffolder owns `.projectID.json` and `.projectStructure.json`. CLIBuilder reads these files but does not create, modify, or repair them.

This extension uses template schema version 2 and project-structure schema version 1. Adding recognized role identifiers does not change either JSON shape, so no schema-version increment is required.

## Recognized roles

| Role | Meaning | CLIBuilder default |
| --- | --- | --- |
| `realityscan.projects` | Directory containing persistent RealityScan `.rsproj` projects. | Save destination for `.rsproj`. |
| `realityscan.alignment.components` | Directory containing persistent RealityScan `.rsalign` alignment components. | Save destination for `.rsalign`. |
| `model.exports` | Directory containing exported models and their accompanying textures. | Export destination for model packages. |

The role identifies a directory by meaning, independently of its name or nesting. File extensions describe the intended CLIBuilder operation; they are not stored in `.projectStructure.json` and do not constrain the directory contents.

`capture.raw` and `capture.processed` remain unchanged for RawEqualizer. CLIBuilder ignores roles it does not consume, and other consumers must ignore the CLIBuilder roles they do not understand.

## Example

```json
{
  "schemaVersion": 1,
  "projectId": "01a043a9-5057-768a-9357-71d2bb4ef041",
  "paths": {
    "capture.raw": "DATASET/raw",
    "capture.processed": "DATASET/processed",
    "realityscan.projects": "REALITYSCAN/projects",
    "realityscan.alignment.components": "REALITYSCAN/alignment-components",
    "model.exports": "EXPORT/models"
  }
}
```

All values are normalized paths relative to the project root and use `/` separators in JSON. The example names and nesting are illustrative and are not part of the contract.

## CLIBuilder consumer behavior

1. Use an explicit path selected by the user when one is supplied.
2. Otherwise, read `.projectID.json` and `.projectStructure.json` from the project root.
3. Accept project-structure schema version 1 only and require its `projectId` to equal the identity document's `projectId`.
4. Select the role appropriate for the requested Save or Export operation.
5. Resolve its value beneath the project root and reject filesystem links. Input directories and files must exist. CLIBuilder Review checks output destinations without creating them; Start may create missing declared output directories and nested output folders after revalidating metadata and checking output collisions. Scaffolder generation and Update metadata retain their own existing-directory rules.
6. If the structure document or requested role is absent, invalid, mismatched, unsafe, or unavailable, do not invent a folder name. Ask the user for a destination or use an explicitly configured CLIBuilder fallback.
7. A user-selected override applies to that CLIBuilder operation and does not rewrite Scaffolder metadata.

Logs, temporary reports, command output, and execution scratch data are outside this contract. CLIBuilder manages them separately from persistent projects, alignment components, models, and textures.

## Scaffolder template and update behavior

- The existing Semantic role selector assigns each recognized role to a folder node in a template v2 document.
- Each role may appear only once per template.
- Generation writes the resolved relative paths into the existing `.projectStructure.json` file.
- Older templates and projects remain valid when none or only some of these roles are present.
- `Update metadata…` requires a saved valid template whose Template ID matches `.projectID.json`.
- Every role directory resolved from that template must already exist; the operation does not create folders or reapply project contents.
- A successful update atomically replaces the role map with the template's current complete role set while preserving the `projectId` from `.projectID.json` unchanged.
- Removing a role from the template removes it from the updated role map, preventing stale default destinations.
