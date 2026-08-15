# Decisions still to confirm

Most initial product decisions are now fixed in `project.md`. Keep this file short and update it as decisions are made.

## Open

1. **Repository scope** — one history repository per `.rfa`, or one repository for a whole folder/library of families? Deliberately undecided until the storage spike.
2. **Variant naming** — what default name is proposed when creating a new variant? Is entering a custom name part of the creation dialog?
3. **Nested families** — mandatory in the first usable semantic diff, or phase immediately after parameters/formulas/types/type values?
4. **Installer format** — lightweight per-user/manual Revit add-in package or MSI? Prefer the simplest offline installation; admin-free is desirable but not mandatory.
5. **Supported Revit 2021 patch levels** — all 2021 updates or a known deployed build set?
6. **Repair/diagnostics** — required in v1 or deferred?
7. **Comparison export** — required later or unnecessary?

## Confirmed

- `.familyhistory` is hidden and stored beside the working `.rfa`.
- `Save version` automatically saves the family first.
- Version note is optional.
- Restore is non-destructive and creates a new version; an automatic restoration note is generated when needed.
- No reset-style branch-tip move in v1.
- `Variant` means the user-facing form of a Git branch. V1 supports create-from-version and switch only.
- Required diff: parameters, formulas, family types, parameter values by type.
- Initial geometry result may be only `Geometry changed`.
- Local single-user only; no shared/network history.
- Revit 2021 only.
- Russian-only UI is acceptable for v1.
- Store complete `.rfa` states; no custom binary delta optimization.
- Family history is moved/copied together with the adjacent history folder.
- Author identity is not stored.
- No telemetry/network/cloud behavior.
