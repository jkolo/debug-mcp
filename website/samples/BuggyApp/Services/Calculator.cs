using BuggyApp.Models;

namespace BuggyApp.Services;

public class Calculator
{
    /// <summary>
    /// Bug: Applies discount as a flat amount instead of a percentage.
    /// For quantity >= 3, should apply 30% discount, but subtracts 30 instead.
    /// Expected: 100 * 3 * 0.70 = 210.00
    /// Actual:   100 * 3 - 30 = 270.00
    /// </summary>
    public decimal CalculateTotal(Order order)
    {
        var subtotal = order.UnitPrice * order.Quantity;

        if (order.Quantity >= 3)
        {
            // Bug: subtracting percentage value instead of multiplying
            var discount = 30; // Should be 0.30m (30%)
            order.Discount = discount;
            return subtotal - discount; // Wrong! Should be: subtotal * (1 - 0.30m)
        }

        return subtotal;
    }
}
