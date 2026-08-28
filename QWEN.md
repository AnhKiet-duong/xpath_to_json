# QWEN.md — XPath to Json Project

## Project Overview

Windows UI automation scanner ("XPathScanner"): scans a running application's UI tree (FlaUI/UIA3) and exports XPath locators as JSON per screen/feature, matching the schema `{name, path, children}` that the user's test-automation system reads.

Implementation status: **v2 complete** (matches real schema samples `KIC*.json`). Authoritative plan: `xpath-scanner-plan (1).md` (v2). `xpath-scanner-plan.md` is the superseded v1 plan. BƯỚC 9 extensions (placeholder `{}`, pick-element-as-root, stale-node cleanup UI, drag & drop reorder, `.diff.json`) are NOT yet implemented.

## Key Files

| File | Purpose |
|------|---------|
| `XPathScanner/XPathScanner.sln` | Solution with 2 projects: `XPathScanner.Core`, `XPathScanner.App` |
| `XPathScanner/XPathScanner.Core/Models/UiNode.cs` | The only model: `{name, path, children}` + `[JsonIgnore]` `Raw*` fields (internal only, never serialized) |
| `XPathScanner/XPathScanner.Core/Services/ProcessListService.cs` | Lists running apps that have a main window (fills the ComboBox) |
| `XPathScanner/XPathScanner.Core/Services/XPathBuilder.cs` | Builds one XPath segment: AutomationId > Name > index, `[@AutomationId="..."]` style (double quotes) |
| `XPathScanner/XPathScanner.Core/Services/UiScannerService.cs` | Recursive UIA scan; collapses transparent containers (no id/name AND exactly 1 child, max 10 in a chain); child paths are relative; suggests semantic names (`Click_`, `Input_`, `Select_`, ...); collects `Warnings` |
| `XPathScanner/XPathScanner.Core/Services/JsonMergeService.cs` | Load/Save/merge JSON; merge key extracted from the `path` string (AutomationId preferred, then Name); preserves user-renamed `name`; NEVER deletes nodes; serializer uses `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` so output is `\"` (not `\u0022`) |
| `XPathScanner/XPathScanner.App/` | WPF UI: app picker + refresh, screen name (required), root anchor path (optional), TreeView, double-click rename via `RenameDialog`, Log pane, Save new / Update existing JSON. `App.xaml.cs` overrides `OnStartup`: if CLI args present → runs `CliRunner` headless and exits (no WPF window); otherwise opens the UI. |
| `XPathScanner/XPathScanner.App/CliRunner.cs` | Headless cmd mode: `XPathScanner.exe list` / `XPathScanner.exe export --app <pid|name> --screen <name> [--root|--out|--merge|--keep-duplicates]`. Attaches to parent console (P/Invoke), scans on an STA thread, writes JSON via `JsonMergeService`. Exit codes: 0 ok / 2 bad args / 3 app or merge file not found / 4 scan error. |
| `XPathScanner-CLI.md` | User guide (Vietnamese) for the cmd export feature. |
| `build.bat` | Builds the solution; `build.bat run` also launches the app |
| `xpath-scanner-plan (1).md` | The v2 implementation plan (authoritative, step-by-step with DoD) |
| `New Noted.json` | Sample v2 scan output (Notepad) — the current output format |
| `test1.json` | STALE v1-format output (flat schema with `buttons`/`inputs`/... groups) — no longer produced; kept only as history |

## Building and Running

- Build: `build.bat` (or `dotnet build XPathScanner\XPathScanner.sln`)
- Build + run: `build.bat run`
- Run manually: `dotnet run --project XPathScanner\XPathScanner.App`
- Requirements: Windows 10/11, .NET SDK 8+ (9.0.312 installed on dev machine).
- FlaUI 5.0.0 packages produce NU1701 warnings (packages target .NET Framework) — known, harmless.
- NOTE: the running app locks `Core.dll` in `App/bin` — close the app before rebuilding.

## Development Conventions

- C# .NET 8; WPF for `App`; class library for `Core`; `System.Text.Json` for (de)serialization.
- **JSON output MUST contain ONLY `name` / `path` / `children`.** The user's automation system parses exactly this schema — never add fields to the export.
- `path` is relative to the nearest ancestor with a non-empty `path`; `path: ""` marks logical/manual action nodes (e.g. `no_action`) — merge keeps them forever.
- Merge (Update existing JSON) NEVER auto-deletes nodes; user-renamed `name` values are preserved across rescans.
- Auto-generated names are suggestions only — the user renames via double-click in the TreeView before saving.
- Keep `dotnet build` green after every change; UI text is Vietnamese.

## Related Context (User's Broader Work)

The user develops document-processing systems that extract structured data from documents (initially fuel receipts): text extraction via a TPS endpoint, followed by AI-model-based (e.g., Qwen3-VL-32B) JSON extraction. The XPath JSON files produced here feed their UI test automation.
