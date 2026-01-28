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

    Scenario: Set exception breakpoint for InvalidOperationException
        Given a running test target process
        And the debugger is attached to the test target
        When I set an exception breakpoint for "System.InvalidOperationException"
        Then the exception breakpoint should be set
        And the exception breakpoint should be for type "System.InvalidOperationException"

    Scenario: Exception breakpoint triggers on throw
        Given a running test target process
        And the debugger is attached to the test target
        When I set an exception breakpoint for "System.InvalidOperationException"
        And the test target executes the "exception" command
        And I wait for a breakpoint hit
        Then the last hit should be an exception breakpoint

    Scenario: List all breakpoints returns set breakpoints
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        And a breakpoint on "LoopTarget.cs" line 17
        When I list all breakpoints
        Then the breakpoint list should contain 2 breakpoints
        And the breakpoint list should contain a breakpoint on "MethodTarget.cs"
        And the breakpoint list should contain a breakpoint on "LoopTarget.cs"

    Scenario: Enable and disable breakpoint toggle
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When I disable the last set breakpoint
        Then the last set breakpoint should be disabled
        When I enable the last set breakpoint
        Then the last set breakpoint should be enabled

    Scenario: Multiple breakpoints hit in order
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 14
        And a breakpoint on "NestedTarget.cs" line 23
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        Then the debugger should pause at "NestedTarget.cs" line 14
        When I continue execution
        And I wait for a breakpoint hit
        Then the debugger should pause at "NestedTarget.cs" line 23

    Scenario: Disabled breakpoint does not trigger
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When I disable the last set breakpoint
        And the test target executes the "method" command
        Then the debugger should not pause within 2 seconds

    Scenario: Remove breakpoint by ID removes from list
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        And a breakpoint on "LoopTarget.cs" line 17
        When I list all breakpoints
        Then the breakpoint list should contain 2 breakpoints
        When I remove the first breakpoint by ID
        And I list all breakpoints
        Then the breakpoint list should contain 1 breakpoints
