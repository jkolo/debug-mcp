Feature: Code Find Usages
    As an LLM agent analyzing code
    I want to find all usages of a symbol across the codebase
    So that I can understand how types, methods, and variables are used

    Background:
        Given the MCP server is running
        And I have loaded TestTargetApp.csproj

    @US1 @P1
    Scenario: Find usages of type by name
        When I call the "code_find_usages" tool with name "TestTargetApp.Person"
        Then the response should be successful
        And the response should contain symbol with name "Person"
        And the response should contain at least 1 usage
        And the usages should include a declaration

    @US1 @P1
    Scenario: Find usages of method by name
        When I call the "code_find_usages" tool with name "TestTargetApp.MethodTarget.SayHello" and symbolKind "Method"
        Then the response should be successful
        And the response should contain symbol with kind "Method"
        And the usages should include a declaration

    @US1 @P1
    Scenario: Find symbol by location
        When I call the "code_find_usages" tool with file "ObjectTarget.cs" line 56 column 14
        Then the response should be successful
        And the response should contain symbol with name "Person"

    @US1 @P1
    Scenario: Symbol not found by name
        When I call the "code_find_usages" tool with name "TestTargetApp.NonExistentClass"
        Then the response should fail with error code "SYMBOL_NOT_FOUND"

    @US1 @P1
    Scenario: No workspace loaded
        Given the workspace is not loaded
        When I call the "code_find_usages" tool with name "TestTargetApp.Person"
        Then the response should fail with error code "NO_WORKSPACE"

    @US1 @P1
    Scenario: Invalid parameters - neither name nor location
        When I call the "code_find_usages" tool with no parameters
        Then the response should fail with error code "INVALID_PARAMETER"

    @US1 @P1
    Scenario: Invalid parameters - both name and location
        When I call the "code_find_usages" tool with name "TestTargetApp.Person" and file "ObjectTarget.cs" line 56 column 14
        Then the response should fail with error code "INVALID_PARAMETER"
