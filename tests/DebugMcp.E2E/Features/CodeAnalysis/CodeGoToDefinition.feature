Feature: Code Go To Definition
    As an LLM agent analyzing code
    I want to navigate to symbol definitions
    So that I can understand where types, methods, and properties are declared

    Background:
        Given the MCP server is running
        And I have loaded TestTargetApp.csproj

    @US5 @P2
    Scenario: Go to method definition
        When I call the "code_goto_definition" tool with file "ObjectTarget.cs" line 25 column 37
        Then the response should be successful
        And the response should contain symbol with name "Name"
        And the definitions should include a source location

    @US5 @P2
    Scenario: Go to class definition
        When I call the "code_goto_definition" tool with file "ObjectTarget.cs" line 10 column 13
        Then the response should be successful
        And the response should contain symbol with name "Person"
        And the definitions should include a source location in "ObjectTarget.cs"

    @US5 @P2
    Scenario: Go to metadata symbol definition
        When I call the "code_goto_definition" tool with file "ObjectTarget.cs" line 27 column 9
        Then the response should be successful
        And the response should contain symbol with name "Console"
        And the definitions should include assembly information

    @US5 @P2
    Scenario: Symbol not found at location
        When I call the "code_goto_definition" tool with file "ObjectTarget.cs" line 1 column 1
        Then the response should fail with error code "SYMBOL_NOT_FOUND"

    @US5 @P2
    Scenario: Invalid file
        When I call the "code_goto_definition" tool with file "/nonexistent/file.cs" line 1 column 1
        Then the response should fail with error code "SYMBOL_NOT_FOUND"

    @US5 @P2
    Scenario: No workspace loaded
        Given the workspace is not loaded
        When I call the "code_goto_definition" tool with file "ObjectTarget.cs" line 25 column 37
        Then the response should fail with error code "NO_WORKSPACE"
