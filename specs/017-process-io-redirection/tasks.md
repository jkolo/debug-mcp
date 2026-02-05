# Tasks: Process I/O Redirection

## Phase 1: ProcessIoManager Service

- [x] T001 Create `ProcessIoManager` class in `DebugMcp/Services/ProcessIoManager.cs`
- [x] T002 Implement stdout/stderr buffer reading with async pump tasks
- [x] T003 Implement stdin writing with `WriteInput` and `CloseInput`
- [x] T004 Register `ProcessIoManager` as singleton in `Program.cs`

## Phase 2: Launch with Redirection

- [x] T005 Modify `ProcessDebugger.LaunchAsync` to use `Process.Start` with redirected I/O
- [x] T006 Wire `ProcessIoManager` to capture stdout/stderr streams
- [x] T007 Keep using DbgShim for runtime startup detection and attach
- [x] T008 Test launch + continue doesn't hang MCP

## Phase 3: New MCP Tools

- [x] T009 Create `ProcessReadOutputTool` with stream filter and clear options
- [x] T010 Create `ProcessWriteInputTool` with data and closeAfter options
- [x] T011 Add tool logging with ToolInvoked/ToolCompleted

## Phase 4: Validation

- [x] T012 Manual test: launch TestTargetApp, continue, read output
- [x] T013 Manual test: write "loop" to stdin, verify loop runs
- [x] T014 Test tracepoint notifications work while process running
