---
id: P0-T01
title: Dựng solution Core, App, Tests build được trên Mac
phase: P0
lane: INFRA
status: doing
deps: []
touches: [Recora.sln, Directory.Build.props, global.json, src/Recora.Core/Recora.Core.csproj, src/Recora.Core/Polyfills.cs, src/Recora.Core/RecoraException.cs, src/Recora.App/Recora.App.csproj, src/Recora.App/app.manifest, src/Recora.App/Program.cs, tests/Recora.Core.Tests/Recora.Core.Tests.csproj, tests/Recora.Core.Tests/SmokeTests.cs]
size: S
---

## Goal
Có khung solution để mọi task sau thêm code vào: Core (netstandard2.0), App (net48 WinForms), Tests (net10.0 xUnit). Build và test chạy được trên Mac.

## Context to read
- AGENTS.md, docs/plan/ARCHITECTURE.md mục 2–3

## Sub-tasks
- [ ] `Directory.Build.props`: `Version`, `LangVersion` 12, `Nullable`, cảnh báo là lỗi
- [ ] Core: csproj, `Polyfills.cs` (`IsExternalInit`), `RecoraException`
- [ ] App: csproj net48 WinExe, `app.manifest` (PerMonitorV2, Windows 10/11), `Program.cs` tối thiểu
- [ ] Tests: xUnit net10.0 tham chiếu Core, một test chạy được

## Acceptance criteria
- [ ] `dotnet build Recora.sln -c Release` xanh trên Mac, không cảnh báo
- [ ] `dotnet test tests/Recora.Core.Tests` xanh

## Security
None.

## How to verify
```bash
dotnet build Recora.sln -c Release && dotnet test tests/Recora.Core.Tests
```

## Out of scope
- Giao diện (P0-T05), FFmpeg (P0-T02)

## Notes / decisions
