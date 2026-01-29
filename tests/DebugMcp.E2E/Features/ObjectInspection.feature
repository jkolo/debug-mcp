Feature: Object Inspection
    As a debugger user
    I want to inspect heap objects and their fields
    So that I can understand object state at runtime

    Background:
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "ObjectTarget.cs" line 25
        When the test target executes the "object" command
        And I wait for a breakpoint hit

    Scenario: Inspect this reference
        When I inspect the object "this"
        Then the object should not be null
        And the object type should contain "ObjectTarget"

    Scenario: Inspect private field
        When I inspect the object "this._currentUser"
        Then the object should not be null
        And the object type should contain "Person"
        And the object should have fields

    Scenario: Inspect nested property
        When I inspect the object "this._currentUser.HomeAddress"
        Then the object should not be null
        And the object type should contain "Address"

    Scenario: Inspect field with depth 2
        When I inspect the object "this._currentUser" with depth 2
        Then the object should not be null
        And the object should have fields

    Scenario: Inspect null property returns isNull true
        When I inspect the object "this._currentUser.WorkAddress"
        Then the object should be null

    Scenario: Read memory at object address
        When I read memory at the address of "this._currentUser" for 64 bytes
        Then the memory result should have bytes
        And the memory result should have the requested size 64

    Scenario: Get type layout for reference type
        When I get the type layout for "TestTargetApp.Person"
        Then the type layout should have name containing "Person"
        And the type should not be a value type
        And the type total size should be greater than 0
