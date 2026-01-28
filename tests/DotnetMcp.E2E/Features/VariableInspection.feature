Feature: Variable Inspection
    As a debugger user
    I want to inspect variables, objects, memory, and types
    So that I can understand program state at a breakpoint

    Background:
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "ObjectTarget.cs" line 25
        When the test target executes the "object" command
        And I wait for a breakpoint hit

    Scenario: Inspect local variables at a breakpoint
        When I inspect local variables
        Then the variables should not be empty

    Scenario: Inspect an object and its fields
        When I inspect the object "this._currentUser"
        Then the object should not be null
        And the object type should contain "Person"
        And the object should have fields

    Scenario: Inspect nested object with depth
        When I inspect the object "this._currentUser" with depth 2
        Then the object should not be null
        And the object should have fields

    Scenario: Inspect a null reference
        When I inspect the object "this._currentUser.WorkAddress"
        Then the object should be null

    Scenario: Read memory at an object address
        When I read memory at the address of "this._currentUser" for 64 bytes
        Then the memory result should have bytes
        And the memory result should have the requested size 64

    Scenario: Read small memory region
        When I read memory at the address of "this._currentUser" for 16 bytes
        Then the memory result should have bytes
        And the memory result actual size should be at most 16

    Scenario: Inspect type layout for a class
        When I get the type layout for "TestTargetApp.Person"
        Then the type layout should have name containing "Person"
        And the type total size should be greater than 0
        And the type should not be a value type

    Scenario: Inspect type layout with inherited fields
        When I get the type layout for "TestTargetApp.Person" including inherited fields
        Then the type layout should have at least 2 fields

    Scenario: Inspect type layout for a value type
        When I get the type layout for "System.Int32"
        Then the type should be a value type
        And the type header size should be 0

    Scenario: Analyze outbound object references
        When I get outbound references for "this._currentUser"
        Then the reference result target type should contain "Person"

    Scenario: Analyze outbound references with limit
        When I get outbound references for "this._currentUser" with max 2 results
        Then the outbound reference count should be at most 2
