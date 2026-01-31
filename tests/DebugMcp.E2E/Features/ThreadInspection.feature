Feature: Thread Inspection
    As a debugger user
    I want to list and inspect managed threads
    So that I can understand thread state in my application

    Scenario: List threads shows at least one thread
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        And I list all threads
        Then the thread list should contain at least 1 threads
        And all threads should have positive IDs

    Scenario: Thread list has a current thread marked
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        And I list all threads
        Then the thread list should not be empty
        And the thread list should have a current thread

    Scenario: Multiple threads visible when spawned
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Libs/Threading/Threading.cs" line 31
        When the test target executes the "threads" command
        And I wait for a breakpoint hit
        And I list all threads
        Then the thread list should contain at least 4 threads
        And all threads should have positive IDs

    Scenario: Thread IDs are unique
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit
        And I list all threads
        Then all thread IDs should be unique
