# Gherkin Step Vocabulary

This defines the reusable Gherkin steps that form the E2E test vocabulary. Step definitions bind these to the debugger API.

## Session Steps

```gherkin
# Given
Given a running test target process
Given the debugger is attached to the test target
Given a launched process paused at entry

# When
When I attach the debugger to the test target
When I detach the debugger
When I launch "{path}" with stop at entry
When I continue execution

# Then
Then the session state should be "{state}"
Then the target process should still be running
```

## Breakpoint Steps

```gherkin
# Given
Given a breakpoint on "{file}" line {int}
Given a conditional breakpoint on "{file}" line {int} with condition "{condition}"

# When
When the test target executes the "{command}" command
When I wait for a breakpoint hit
When I remove the breakpoint

# Then
Then the debugger should pause at "{file}" line {int}
Then the breakpoint hit count should be {int}
Then the debugger should not pause within {int} seconds
```

## Stepping Steps

```gherkin
# When
When I step over
When I step into
When I step out

# Then
Then the debugger should be at "{file}" line {int}
Then the debugger should be in method "{methodName}"
```

## Inspection Steps

```gherkin
# When
When I inspect local variables
When I inspect the object "{variableName}"
When I evaluate the expression "{expression}"
When I request the stack trace

# Then
Then the variables should contain "{name}" with value "{value}"
Then the variables should contain "{name}" of type "{type}"
Then the object should have field "{fieldName}" with value "{value}"
Then the expression result should be "{value}"
Then the expression result type should be "{type}"
Then the stack trace should contain {int} frames
Then the stack trace should contain method "{methodName}"
```
