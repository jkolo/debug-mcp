Feature: Session Edge Cases
    As a debugger user
    I want session management to handle edge cases gracefully
    So that error conditions don't crash or hang the debugger

    Scenario: Query debug state with no session returns Disconnected
        Then the session state should be "Disconnected"

    Scenario: Debug state returns correct info after attach
        Given a running test target process
        When I attach the debugger to the test target
        Then the session state should be "Running"
        And the process ID should be positive

    Scenario: Continue on running process returns error
        Given a running test target process
        And the debugger is attached to the test target
        Then continuing execution should fail with "not paused"

    Scenario: Pause running process changes state to paused
        Given a running test target process
        And the debugger is attached to the test target
        When I pause execution
        Then the session state should be "Paused"
        And the session pause reason should be "Pause"

    Scenario: Detach twice does not throw
        Given a running test target process
        And the debugger is attached to the test target
        When I detach the debugger
        Then the session state should be "Disconnected"
        When I detach the debugger
        Then the session state should be "Disconnected"
