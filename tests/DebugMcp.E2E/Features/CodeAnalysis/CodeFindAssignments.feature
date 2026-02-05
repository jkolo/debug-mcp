Feature: Code Find Assignments
    As an LLM agent analyzing code
    I want to find all assignments to a variable, field, or property
    So that I can understand where values are being written

    Background:
        Given the MCP server is running
        And I have loaded TestTargetApp.csproj

    @US2 @P1
    Scenario: Find field assignments by name
        When I call the "code_find_assignments" tool with name "TestTargetApp.ObjectTarget._currentUser"
        Then the response should be successful
        And the response should contain symbol with name "_currentUser"
        And the response should contain at least 1 assignment

    @US2 @P1
    Scenario: Find property assignments with initializer
        When I call the "code_find_assignments" tool with name "TestTargetApp.Person.Name" and symbolKind "Property"
        Then the response should be successful
        And the response should contain assignment with kind "Initializer"

    @US2 @P1
    Scenario: Find loop variable assignments including increment
        When I call the "code_find_assignments" tool with file "LoopTarget.cs" line 14 column 18
        Then the response should be successful
        And the response should contain assignment with kind "Declaration"
        And the response should contain assignment with kind "Increment"

    @US2 @P1
    Scenario: Symbol not found
        When I call the "code_find_assignments" tool with name "TestTargetApp.NonExistent"
        Then the response should fail with error code "SYMBOL_NOT_FOUND"

    @US2 @P1
    Scenario: No workspace loaded
        Given the workspace is not loaded
        When I call the "code_find_assignments" tool with name "TestTargetApp.ObjectTarget._currentUser"
        Then the response should fail with error code "NO_WORKSPACE"
