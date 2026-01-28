Feature: Module Enumeration
    As a debugger user
    I want to list all loaded modules after attaching to a process
    So that I can set breakpoints in any loaded assembly

    Scenario: All modules are visible after attach
        Given a running test target process
        And the debugger is attached to the test target
        When I list all modules without system filter
        Then the module list should contain "TestTargetApp"
        And the module list should contain "BaseTypes"
        And the module list should contain "Collections"
        And the module list should contain "Exceptions"
        And the module list should contain "Recursion"
        And the module list should contain "Expressions"
        And the module list should contain "Threading"
        And the module list should contain "AsyncOps"
        And the module list should contain "MemoryStructs"
        And the module list should contain "ComplexObjects"
        And the module list should contain "Scenarios"
