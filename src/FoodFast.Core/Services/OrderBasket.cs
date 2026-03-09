namespace FoodFast.Core.Services;

/// <summary>
/// Tracks items in a customer's active order basket.
/// Invariant: Total must always equal the sum of items currently in the basket.
/// Demonstrated via Stateful PBT with random action sequences.
/// </summary>
public class OrderBasket
{
    private readonly List<decimal> _items = new();

    public decimal Total { get; private set; }
    public int     Count => _items.Count;

    public void AddItem(decimal price)
    {
        _items.Add(price);
        Total += price;
    }

    public void RemoveLastItem()
    {
        if (_items.Count == 0) return;
        decimal last = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        Total -= last;
    }

    public void Clear()
    {
        _items.Clear();
        Total = 0;
    }
}
