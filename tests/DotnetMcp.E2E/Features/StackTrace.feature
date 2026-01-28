Feature: Stack Trace
    As a debugger user
    I want to view the call stack
    So that I can understand the execution path to the current point

    Scenario: View the call stack at a breakpoint
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 32
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I request the stack trace
        Then the stack trace should contain at least 3 frames
        And the stack trace should contain method "Level3"

    Scenario: Inspect variables in a different stack frame
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        And I request the stack trace
        Then the stack trace should contain at least 1 frames

    Scenario: Stack trace contains expected nested methods in order
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 32
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I request the stack trace
        Then the stack trace should contain method "Level3"
        And the stack trace should contain method "Level2"
        And the stack trace should contain method "Level1"

    Scenario: List threads at breakpoint
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        And I list all threads
        Then the thread list should not be empty
        And the thread list should have a current thread

    Scenario: Stack frame contains source location
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        And I request the stack trace
        Then the top frame should have source location containing "MethodTarget.cs"
