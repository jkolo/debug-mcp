Feature: Expression Evaluation
    As a debugger user
    I want to evaluate expressions in the debugger context
    So that I can examine and compute values at breakpoints

    Background:
        Given a running test target process
        And the debugger is attached to the test target
        And a breakpoint on "Libs/Expressions/Expressions.cs" line 31
        When the test target executes the "expressions" command
        And I wait for a breakpoint hit

    Scenario: Evaluate 'this' reference
        When I evaluate the expression "this"
        Then the evaluation result type should contain "ExpressionTarget"

    Scenario: Evaluate property on this
        When I evaluate the expression "this.Value"
        Then the evaluation result value should be "42"

    Scenario: Evaluate string property on this
        When I evaluate the expression "this.Name"
        Then the evaluation result value should contain "TestTarget"

    Scenario: Evaluate nested property access
        When I evaluate the expression "this.Inner.Value"
        Then the evaluation result value should be "100"

    Scenario: Evaluate deeply nested property
        When I evaluate the expression "this.Inner.Name"
        Then the evaluation result value should contain "InnerTarget"
