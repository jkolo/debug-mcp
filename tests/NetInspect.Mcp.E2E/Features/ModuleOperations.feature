Feature: Module Operations
    As a debugger user
    I want to search and browse types and members in loaded modules
    So that I can understand the codebase structure at runtime

    Background:
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "MethodTarget.cs" line 14
        When the test target executes the "method" command
        And I wait for a breakpoint hit

    Scenario: Search for types across modules
        When I search for types matching "Person"
        Then the search result should not be empty
        And the search result should contain type "Person"

    Scenario: Search for types with wildcard
        When I search for types matching "Base*"
        Then the search result should not be empty

    Scenario: Get types in a specific module
        When I get types in module "TestTargetApp"
        Then the types result should not be empty
        And the types result should contain type "ObjectTarget"

    Scenario: Get types filtered by namespace
        When I get types in module "TestTargetApp" with namespace filter "TestTargetApp"
        Then the types result should not be empty

    Scenario: Get members of a type
        When I get members of type "TestTargetApp.Person"
        Then the members result should not be empty
        And the members result should contain member "Name"
        And the members result should contain member "Age"

    Scenario: Get methods only from a type
        When I get methods of type "TestTargetApp.NestedTarget"
        Then the members result should not be empty
        And the members result should contain member "Level1"
        And the members result should contain member "Level2"
        And the members result should contain member "Level3"

    Scenario: Module list includes system modules when requested
        When I list all modules including system modules
        Then the module list should contain "System.Private.CoreLib"
