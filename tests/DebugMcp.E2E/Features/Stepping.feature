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

    Scenario: Step over multiple times in loop
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "LoopTarget.cs" line 17
        When the test target executes the "loop" command
        And I wait for a breakpoint hit
        And I step over
        And I step over
        And I step over
        Then the session state should be "Paused"

    Scenario: Step into nested method verifies location change
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 14
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I step into
        Then the current stack frame should be in method "Level2"

    Scenario: Step out returns to caller
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 23
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I step out
        Then the current stack frame should be in method "Level1"

    Scenario: Step out from deep nested call returns to each caller
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 32
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I step out
        Then the current stack frame should be in method "Level2"
        When I step out
        Then the current stack frame should be in method "Level1"

    Scenario: Step into cross-assembly call
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Libs/Recursion/Recursion.cs" line 20
        When the test target executes the "recurse" command
        And I wait for a breakpoint hit
        And I step into
        Then the session state should be "Paused"
        And the session pause reason should be "Step"

    Scenario: Step out from recursive call
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Libs/Recursion/Recursion.cs" line 16
        When the test target executes the "recurse" command
        And I wait for a breakpoint hit
        And I step out
        Then the current stack frame should be in method "Factorial"
        And the session pause reason should be "Step"

    Scenario: Step over preserves state after multiple steps
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "LoopTarget.cs" line 17
        When the test target executes the "loop" command
        And I wait for a breakpoint hit
        And I step over
        And I step over
        And I step over
        And I step over
        And I step over
        Then the session state should be "Paused"
        And the session pause reason should be "Step"

    Scenario: Step into then step out round-trips
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 14
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I step into
        Then the current stack frame should be in method "Level2"
        When I step out
        Then the current stack frame should be in method "Level1"
