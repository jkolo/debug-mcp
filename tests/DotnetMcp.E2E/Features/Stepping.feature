Feature: Stepping
    As a debugger user
    I want to step through code line by line
    So that I can understand program execution flow

    Scenario: Step over a line of code
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "LoopTarget.cs" line 17
        When the test target executes the "loop" command
        And I wait for a breakpoint hit
        And I step over
        Then the session state should be "Paused"
        And the session pause reason should be "Step"

    Scenario: Step into a method call
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 14
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I step into
        Then the session state should be "Paused"
        And the session pause reason should be "Step"

    Scenario: Step out of a method
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 32
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I step out
        Then the session state should be "Paused"
        And the session pause reason should be "Step"

    Scenario: Continue execution after breakpoint hit
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        And I continue execution
        Then the session state should be "Running"

    Scenario: Continue when not paused throws error
        Given a running test target process
        And the debugger is attached to the test target
        Then continuing execution should fail with "not paused"

    Scenario: Step when not paused throws error
        Given a running test target process
        And the debugger is attached to the test target
        Then stepping over should fail with "not paused"
