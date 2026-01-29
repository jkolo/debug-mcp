namespace BuggyApp.Models;

public class Order
{
    public string OrderId { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
    public string Status { get; set; } = "Pending";

    public Order(string orderId, decimal unitPrice, int quantity)
    {
        OrderId = orderId;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Discount = 0;
    }
}
