using BuggyApp.Models;

namespace BuggyApp.Services;

public class OrderService
{
    private readonly UserService _userService;

    public OrderService(UserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Bug: Doesn't check if user is active before processing.
    /// Throws InvalidOperationException for inactive users,
    /// but the error message is misleading ("User not found" instead of "User inactive").
    /// </summary>
    public Order ProcessOrder(string userId, string orderId)
    {
        var user = _userService.GetUser(userId);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        // Bug: checks IsActive but throws wrong message
        if (!user.IsActive)
            throw new InvalidOperationException($"User {userId} not found"); // Should say "inactive"

        var order = new Order(orderId, 50.00m, 1);
        order.Status = "Processing";
        return order;
    }
}
