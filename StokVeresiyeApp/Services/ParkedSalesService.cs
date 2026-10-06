using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public static class ParkedSalesService
{
    private static readonly List<ParkedSaleModel> _parkedList = new();
    public static event Action? ParkedSalesChanged;

    public static IReadOnlyList<ParkedSaleModel> GetAll()
    {
        lock (_parkedList)
        {
            return _parkedList.OrderByDescending(p => p.ParkedAt).ToList();
        }
    }

    public static int Count
    {
        get
        {
            lock (_parkedList)
            {
                return _parkedList.Count;
            }
        }
    }

    public static void Park(ParkedSaleModel sale)
    {
        lock (_parkedList)
        {
            _parkedList.Add(sale);
        }
        ParkedSalesChanged?.Invoke();
    }

    public static ParkedSaleModel? Pop(string id)
    {
        lock (_parkedList)
        {
            var item = _parkedList.FirstOrDefault(p => p.Id == id);
            if (item != null)
            {
                _parkedList.Remove(item);
                ParkedSalesChanged?.Invoke();
                return item;
            }
        }
        return null;
    }

    public static void Remove(string id)
    {
        lock (_parkedList)
        {
            _parkedList.RemoveAll(p => p.Id == id);
        }
        ParkedSalesChanged?.Invoke();
    }
}
