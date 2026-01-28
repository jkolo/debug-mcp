// Test target for debugger attach/launch and breakpoint tests
using TestTargetApp;

// Force-load all test libraries via transitive dependency chain
// Scenarios -> ComplexObjects -> Expressions,AsyncOps,MemoryStructs -> Collections,Exceptions,Recursion,Threading -> BaseTypes
var topLib = Scenarios.ScenariosUtil.GetName();

Console.WriteLine($"TestTargetApp started. PID: {Environment.ProcessId}. Top lib: {topLib}");
Console.WriteLine("READY");
Console.Out.Flush();

// Command loop for test orchestration
while (true)
{
    var command = Console.ReadLine();
    if (string.IsNullOrEmpty(command) || command == "exit")
        break;

    switch (command)
    {
        case "loop":
            // Run a simple loop - breakpoints can be set on LoopTarget.RunLoop
            LoopTarget.RunLoop(5);
            Console.WriteLine("LOOP_DONE");
            Console.Out.Flush();
            break;

        case "method":
            // Call a method - breakpoints can be set on MethodTarget.SayHello
            var result = MethodTarget.SayHello("World");
            Console.WriteLine($"METHOD_RESULT:{result}");
            Console.Out.Flush();
            break;

        case "exception":
            // Throw an exception - for exception breakpoint testing
            try
            {
                ExceptionTarget.ThrowException();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"EXCEPTION_CAUGHT:{ex.Message}");
                Console.Out.Flush();
            }
            break;

        case "nested":
            // Call nested methods - for stack trace testing
            NestedTarget.Level1();
            Console.WriteLine("NESTED_DONE");
            Console.Out.Flush();
            break;

        case "object":
            // Create and process object - for nested property inspection testing
            var objectTarget = new ObjectTarget("TestUser");
            objectTarget.ProcessUser();
            Console.WriteLine("OBJECT_DONE");
            Console.Out.Flush();
            break;

        case "deep":
            // 5-level nesting test - for deep property chain testing (T044)
            var deepTarget = new DeepNestingTarget();
            deepTarget.ProcessCompany();
            Console.WriteLine("DEEP_DONE");
            Console.Out.Flush();
            break;

        case "recurse":
            // Deep recursion test - for stack trace depth testing
            var factorial = Recursion.RecursiveCalculator.Factorial(10);
            Console.WriteLine($"RECURSE_RESULT:{factorial}");
            Console.Out.Flush();
            break;

        case "threads":
            // Multi-thread test - for thread inspection testing
            Threading.ThreadSpawner.SpawnAndWait(3);
            Console.WriteLine("THREADS_DONE");
            Console.Out.Flush();
            break;

        case "collections":
            // Collection test - for variable inspection of List, Dictionary, array
            var holder = Collections.CollectionHolder.Create();
            Console.WriteLine($"COLLECTIONS_CREATED:List={holder.StringList.Count},Dict={holder.IntMap.Count},Array={holder.Numbers.Length}");
            Console.Out.Flush();
            break;

        case "expressions":
            // Expression target test - for expression evaluation testing
            var exprTarget = Expressions.ExpressionTarget.Create();
            // TestExpressions has local variables for evaluation tests (breakpoint on line 31)
            var testResult = exprTarget.TestExpressions(5);
            Console.WriteLine($"EXPRESSIONS_RESULT:{exprTarget.Name},{exprTarget.Value},{testResult}");
            Console.Out.Flush();
            break;

        case "structs":
            // Struct test - for memory layout and struct inspection
            var layoutStruct = MemoryStructs.LayoutStruct.Create();
            Console.WriteLine($"STRUCTS_CREATED:Id={layoutStruct.Id},Value={layoutStruct.Value},Flag={layoutStruct.Flag}");
            Console.Out.Flush();
            break;

        case "enums":
            // Enum and nullable test - for type inspection
            var testEnum = BaseTypes.TestEnum.Green;
            var nullableHolder = BaseTypes.NullableHolder.CreateWithNulls();
            Console.WriteLine($"ENUMS_CREATED:Enum={testEnum},NullableInt={nullableHolder.NullableInt?.ToString() ?? "null"}");
            Console.Out.Flush();
            break;

        case "complex":
            // Complex objects test - for deep object inspection
            var deepObj = ComplexObjects.DeepObject.CreateChain(5);
            Console.WriteLine($"COMPLEX_CREATED:Level={deepObj.Level}");
            Console.Out.Flush();
            break;

        default:
            Console.WriteLine($"UNKNOWN_COMMAND:{command}");
            Console.Out.Flush();
            break;
    }
}

Console.WriteLine("TestTargetApp exiting.");
