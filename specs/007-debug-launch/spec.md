# Feature Specification: Debug Launch

**Feature Branch**: `007-debug-launch`
**Created**: 2026-01-27
**Status**: Draft
**Input**: User description: "Implement debug_launch functionality to start .NET processes under debugger control using DbgShim.CreateProcessForLaunch and RegisterForRuntimeStartup. Currently throws NotImplementedException. Should support: launching DLL with dotnet runtime, stopAtEntry flag, command line arguments, working directory, environment variables."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Launch Application for Debugging (Priority: P1)

As a developer, I want to launch a .NET application under debugger control so that I can debug it from the very beginning of execution, including initialization code.

**Why this priority**: This is the core functionality - without being able to launch a process, the feature has no value. Launching and attaching is the minimum viable capability.

**Independent Test**: Launch a .NET console application DLL, verify the process starts and debugger is connected, then disconnect and confirm process terminates or continues based on settings.

**Acceptance Scenarios**:

1. **Given** a valid .NET DLL path, **When** I invoke debug_launch with the path, **Then** the process starts and debugger attaches successfully, returning session details including process ID.

2. **Given** a non-existent DLL path, **When** I invoke debug_launch, **Then** I receive a clear error indicating the file was not found.

3. **Given** an invalid file (not a .NET assembly), **When** I invoke debug_launch, **Then** I receive an error indicating the file is not a valid .NET application.

---

### User Story 2 - Stop at Entry Point (Priority: P2)

As a developer, I want the option to pause execution at the entry point so that I can set breakpoints before any user code runs.

**Why this priority**: Essential for debugging initialization issues. Without this, developers cannot debug code that runs at startup.

**Independent Test**: Launch application with stopAtEntry=true, verify process is paused immediately, set a breakpoint, continue execution, verify breakpoint is hit.

**Acceptance Scenarios**:

1. **Given** stopAtEntry is true (default), **When** I launch an application, **Then** execution pauses at the entry point before any user code runs, with session state showing "paused" and pause reason "entry".

2. **Given** stopAtEntry is false, **When** I launch an application, **Then** execution starts immediately without pausing, with session state showing "running".

---

### User Story 3 - Pass Command Line Arguments (Priority: P3)

As a developer, I want to pass command line arguments to the launched application so that I can test different execution paths and configurations.

**Why this priority**: Many applications require arguments to function correctly. Testing without arguments limits debugging usefulness.

**Independent Test**: Launch application with specific arguments, verify the application receives and can access those arguments.

**Acceptance Scenarios**:

1. **Given** command line arguments ["--verbose", "--config", "debug.json"], **When** I launch the application, **Then** the application receives these arguments in the correct order.

2. **Given** arguments with spaces and special characters, **When** I launch the application, **Then** arguments are properly escaped and passed correctly.

---

### User Story 4 - Set Working Directory (Priority: P4)

As a developer, I want to specify the working directory for the launched process so that relative paths in my application resolve correctly.

**Why this priority**: Necessary for applications that depend on relative file paths. Less critical than basic launch and arguments.

**Independent Test**: Launch application with specific working directory, verify the process's current directory matches the specified path.

**Acceptance Scenarios**:

1. **Given** a valid working directory path, **When** I launch the application with cwd parameter, **Then** the process starts with that directory as its current working directory.

2. **Given** no working directory specified, **When** I launch the application, **Then** the process uses a sensible default (the DLL's directory or current directory).

3. **Given** an invalid/non-existent working directory, **When** I launch the application, **Then** I receive a clear error indicating the directory does not exist.

---

### User Story 5 - Set Environment Variables (Priority: P5)

As a developer, I want to set environment variables for the launched process so that I can test environment-dependent behavior.

**Why this priority**: Useful for testing different configurations but can often be worked around by modifying the application.

**Independent Test**: Launch application with custom environment variables, verify the application can read those variables.

**Acceptance Scenarios**:

1. **Given** environment variables {"ASPNETCORE_ENVIRONMENT": "Development", "MY_VAR": "test"}, **When** I launch the application, **Then** the process has access to these environment variables.

2. **Given** environment variables that override system defaults, **When** I launch the application, **Then** the custom values take precedence for the launched process.

---

### Edge Cases

- What happens when the DLL requires a specific .NET runtime version that is not installed?
- How does the system handle launching when another debug session is already active?
- What happens if the process exits immediately after launch (before debugger fully attaches)?
- How does the system handle DLLs that are locked by another process?
- What happens when launching with insufficient permissions (e.g., admin-required application)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST launch .NET applications (DLLs) using the appropriate dotnet runtime
- **FR-002**: System MUST attach the debugger to the launched process before user code executes
- **FR-003**: System MUST support the stopAtEntry flag to pause at entry point (default: true)
- **FR-004**: System MUST support passing command line arguments to the launched process
- **FR-005**: System MUST support specifying a working directory for the launched process
- **FR-006**: System MUST support setting custom environment variables for the launched process
- **FR-007**: System MUST return detailed session information upon successful launch (process ID, state, executable path)
- **FR-008**: System MUST validate the program path exists before attempting launch
- **FR-009**: System MUST reject launch requests when a debug session is already active
- **FR-010**: System MUST support a configurable timeout for the launch operation
- **FR-011**: System MUST properly clean up resources if launch fails partway through
- **FR-012**: When disconnecting from a launched process, system MUST offer option to terminate or let it continue

### Key Entities

- **LaunchedProcess**: Represents a process started by the debugger. Contains: process ID, executable path, command line arguments, working directory, environment variables, launch time.
- **DebugSession**: Extended to track launch mode (attach vs launch) and store launch parameters for session reconstruction.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Launching a simple .NET console application completes in under 5 seconds
- **SC-002**: When stopAtEntry is true, process is paused before any user code executes (verified by no console output before explicit continue)
- **SC-003**: All provided command line arguments are accessible to the launched application in correct order
- **SC-004**: Working directory is correctly set as verified by the launched process
- **SC-005**: Environment variables are accessible to the launched process
- **SC-006**: Clear, actionable error messages for all failure scenarios (file not found, invalid assembly, permission denied)
- **SC-007**: Launch operation can be cancelled via timeout without leaving orphan processes

## Assumptions

- The target system has .NET runtime installed and accessible via PATH or known locations
- DbgShim library is available (already a dependency for attach functionality)
- The user has necessary permissions to launch and debug processes
- Target DLLs are self-contained or have their dependencies available

## Out of Scope

- Launching native (non-.NET) executables
- Remote process launching (launching on a different machine)
- Launching with elevated privileges (run as administrator)
- Attaching to processes that were launched externally and then connecting
- IDE integration (VS Code, Rider launch configurations)
