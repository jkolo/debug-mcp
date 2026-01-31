Feature: Complex Type Inspection
    As a debugger user
    I want to inspect complex variable types like collections, enums, and nullables
    So that I can understand diverse program state at breakpoints

    Scenario: Inspect variables at collections breakpoint
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Program.cs" line 88
        When the test target executes the "collections" command
        And I wait for a breakpoint hit
        And I inspect local variables
        Then the variables should not be empty
        And a variable with type containing "CollectionHolder" should exist

    Scenario: Inspect collection holder object has fields
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Program.cs" line 88
        When the test target executes the "collections" command
        And I wait for a breakpoint hit
        And I inspect local variables
        Then the variables should not be empty
        And the variable count should be at least 1

    Scenario: Inspect enum variables at breakpoint
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Program.cs" line 112
        When the test target executes the "enums" command
        And I wait for a breakpoint hit
        And I inspect local variables
        Then the variables should not be empty
        And the variable count should be at least 2

    Scenario: Enum variables have expected count
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Program.cs" line 112
        When the test target executes the "enums" command
        And I wait for a breakpoint hit
        And I inspect local variables
        Then the variables should not be empty
        And the variable count should be at least 1

    Scenario: Variables at enum breakpoint have types
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Program.cs" line 112
        When the test target executes the "enums" command
        And I wait for a breakpoint hit
        And I inspect local variables
        Then the variables should not be empty
        And all variables should have a type

    Scenario: Inspect struct type layout
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Program.cs" line 104
        When the test target executes the "structs" command
        And I wait for a breakpoint hit
        When I get the type layout for "MemoryStructs.LayoutStruct"
        Then the type layout should have name containing "LayoutStruct"
        And the type total size should be greater than 0
        And the type should be a value type
