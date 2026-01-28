Feature: Breakpoints
    As a debugger user
    I want to set and manage breakpoints
    So that I can pause execution at specific code locations

    Scenario: Set a breakpoint and hit it during execution
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        Then the debugger should pause at "MethodTarget.cs" line 14
        And the session state should be "Paused"

    Scenario: Conditional breakpoint with hit count
        Given a running test target process
        And the debugger is attached to the test target
        And a conditional breakpoint on "LoopTarget.cs" line 17 with condition "hitCount == 3"
        When the test target executes the "loop" command
        And I wait for a breakpoint hit
        Then the breakpoint hit count should be 3

    Scenario: Remove a breakpoint
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When I remove the breakpoint
        And the test target executes the "method" command
        Then the debugger should not pause within 3 seconds

    Scenario: Wait for breakpoint times out when no hit occurs
        Given a running test target process
        And the debugger is attached to the test target
        Then the debugger should not pause within 1 seconds
