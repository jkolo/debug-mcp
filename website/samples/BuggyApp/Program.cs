// BuggyApp — sample .NET application with intentional bugs for debugging demos.
// Used in asciinema recording scenarios for debug-mcp documentation.

using BuggyApp.Services;
using BuggyApp.Models;

Console.WriteLine("BuggyApp starting...");

var userService = new UserService();
var calculator = new Calculator();

// Bug 1: NullReferenceException — GetUser returns null for unknown IDs
try
{
    var user = userService.GetUser("unknown-id");
    Console.WriteLine($"User name length: {user.Name.Length}"); // NRE here
}
catch (NullReferenceException ex)
{
    Console.WriteLine($"Bug 1 triggered: {ex.Message}");
}

// Bug 2: Logic error — discount calculation is wrong
var order = new Order("ORD-001", 100.00m, 3);
var total = calculator.CalculateTotal(order);
Console.WriteLine($"Order total (expected 70.00): {total}");

// Bug 3: Exception in a service with multiple layers
try
{
    var orderService = new OrderService(userService);
    orderService.ProcessOrder("user-123", "ORD-002");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Bug 3 triggered: {ex.Message}");
}

Console.WriteLine("BuggyApp finished.");
