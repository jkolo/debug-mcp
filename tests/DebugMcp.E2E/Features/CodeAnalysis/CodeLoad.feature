Feature: Code Load
    As an LLM agent starting code analysis
    I want to load a solution or project file
    So that Roslyn can parse and understand the codebase for subsequent queries

    Background:
        Given the MCP server is running

    @US4 @P1
    Scenario: Load valid solution
        When I call the "code_load" tool with path to TestTargetApp.sln
        Then the response should be successful
        And the response should contain workspace type "Solution"
        And the response should contain at least 1 project
        And the response should contain "loaded_at" timestamp

    @US4 @P1
    Scenario: Load valid project
        When I call the "code_load" tool with path to TestTargetApp.csproj
        Then the response should be successful
        And the response should contain workspace type "Project"
        And the response should contain exactly 1 project

    @US4 @P1
    Scenario: Load invalid path
        When I call the "code_load" tool with path "/nonexistent/solution.sln"
        Then the response should fail with error code "INVALID_PATH"

    @US4 @P1
    Scenario: Replace existing workspace
        Given I have loaded TestTargetApp.sln
        When I call the "code_load" tool with path to TestTargetApp.sln again
        Then the response should be successful
        And the workspace should be replaced with new load timestamp
