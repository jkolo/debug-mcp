Feature: Code Get Diagnostics
    As an LLM agent analyzing code
    I want to retrieve compilation diagnostics
    So that I can identify errors and warnings in the codebase

    Background:
        Given the MCP server is running
        And I have loaded TestTargetApp.csproj

    @US3 @P2
    Scenario: Get all diagnostics
        When I call the "code_get_diagnostics" tool
        Then the response should be successful
        And the response should contain "total_count"
        And the response should contain "diagnostics" array

    @US3 @P2
    Scenario: Get diagnostics for specific project
        When I call the "code_get_diagnostics" tool with projectName "TestTargetApp"
        Then the response should be successful
        And all diagnostics should have project "TestTargetApp"

    @US3 @P2
    Scenario: Get only errors
        When I call the "code_get_diagnostics" tool with minSeverity "Error"
        Then the response should be successful
        And all diagnostics should have severity "Error"

    @US3 @P2
    Scenario: Limit results
        When I call the "code_get_diagnostics" tool with maxResults 5
        Then the response should be successful
        And the diagnostics count should be at most 5

    @US3 @P2
    Scenario: Invalid project name
        When I call the "code_get_diagnostics" tool with projectName "NonExistentProject"
        Then the response should fail with error code "PROJECT_NOT_FOUND"

    @US3 @P2
    Scenario: No workspace loaded
        Given the workspace is not loaded
        When I call the "code_get_diagnostics" tool
        Then the response should fail with error code "NO_WORKSPACE"
