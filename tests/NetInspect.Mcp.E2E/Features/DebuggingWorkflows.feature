Feature: Debugging Workflows
    As a debugger user
    I want to execute multi-step debugging operations
    So that I can investigate complex program behavior

    Scenario: Set breakpoint, hit, inspect variables, step, inspect again
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "LoopTarget.cs" line 13
        When the test target executes the "loop" command
        And I wait for a breakpoint hit
        And I inspect local variables
        Then the variables should not be empty
        When I step over
        And I inspect local variables
        Then the variables should not be empty

    Scenario: Continue after inspection and hit next breakpoint
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 14
        And a breakpoint on "NestedTarget.cs" line 23
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        Then the current stack frame should be in method "Level1"
        When I continue execution
        And I wait for a breakpoint hit
        Then the current stack frame should be in method "Level2"

    Scenario: Deep call stack inspection shows full call chain
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "NestedTarget.cs" line 32
        When the test target executes the "nested" command
        And I wait for a breakpoint hit
        And I request the stack trace
        Then the stack trace should contain at least 3 frames
        And the stack trace should contain method "Level3"
        And the stack trace should contain method "Level2"
        And the stack trace should contain method "Level1"

    Scenario: Object inspection followed by memory read
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "ObjectTarget.cs" line 25
        When the test target executes the "object" command
        And I wait for a breakpoint hit
        When I inspect the object "this._currentUser"
        Then the object should not be null
        And the object type should contain "Person"
        When I read memory at the address of "this._currentUser" for 32 bytes
        Then the memory result should have bytes

    Scenario: First breakpoint hit has count of one
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "LoopTarget.cs" line 13
        When the test target executes the "loop" command
        And I wait for a breakpoint hit
        Then the breakpoint hit count should be 1

    Scenario: Type inspection followed by member exploration
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "ObjectTarget.cs" line 25
        When the test target executes the "object" command
        And I wait for a breakpoint hit
        When I get the type layout for "TestTargetApp.Person"
        Then the type layout should have name containing "Person"
        When I get members of type "TestTargetApp.Person"
        Then the type should have at least 2 members

    Scenario: Exception breakpoint hits on thrown exception
        Given a running test target process
        And the debugger is attached to the test target
        When I set an exception breakpoint for "System.InvalidOperationException"
        And the test target executes the "exception" command
        And I wait for a breakpoint hit
        Then the breakpoint hit should be an exception
