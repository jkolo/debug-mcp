Feature: Session Lifecycle
    As a debugger user
    I want to attach to, launch, and detach from processes
    So that I can control the debug session lifecycle

    Scenario: Attach to a running process
        Given a running test target process
        When I attach the debugger to the test target
        Then the session state should be "Running"
        And the target process should still be running

    Scenario: Detach from a debug session
        Given a running test target process
        And the debugger is attached to the test target
        When I detach the debugger
        Then the session state should be "Disconnected"
        And the target process should still be running

    Scenario: Launch a process paused at entry
        When I launch the test target with stop at entry
        Then the session state should be "Paused"
        And the session pause reason should be "Entry"
        And the process ID should be positive
        And the session should have launch mode "Launch"

    Scenario: Continue execution after launch pause
        Given a launched process paused at entry
        When I continue execution
        Then the session state should be "Running"

    Scenario: Operations on disconnected session fail gracefully
        Given a running test target process
        And the debugger is attached to the test target
        When I detach the debugger
        Then getting stack trace should fail with "No active debug session"

    Scenario: Get session state when not attached
        Then the session state should be "Disconnected"
