using System;
using PopLife.Runtime;


namespace PopLife.Customers.Services
{
    public static class CustomerEventBus
    {
        public static event Action<PopLife.Customers.Runtime.CustomerAgent> OnSpawned;
        public static event Action<string> OnBuildingChanged; // buildingId
        public static event Action<string> OnShelfSoldOut; // shelfId
        public static event Action OnEEBChanged;

        // 新增交互事件
        public static event Action<PopLife.Customers.Runtime.CustomerAgent, ShelfInstance> OnReachedShelf;
        public static event Action<PopLife.Customers.Runtime.CustomerAgent, FacilityInstance> OnReachedCashier;
        public static event Action<PopLife.Customers.Runtime.CustomerAgent, ShelfInstance, int, int> OnPurchased; // agent, shelf, quantity, price
        public static event Action<PopLife.Customers.Runtime.CustomerAgent> OnCheckedOut;
        public static event Action<PopLife.Customers.Runtime.CustomerAgent> OnCustomerDestroyed; // 顾客销毁事件


        public static void RaiseSpawned(PopLife.Customers.Runtime.CustomerAgent a) => OnSpawned?.Invoke(a);
        public static void RaiseBuildingChanged(string id) => OnBuildingChanged?.Invoke(id);
        public static void RaiseShelfSoldOut(string id) => OnShelfSoldOut?.Invoke(id);
        public static void RaiseEEBChanged() => OnEEBChanged?.Invoke();

        // 新增事件触发方法
        public static void RaiseReachedShelf(PopLife.Customers.Runtime.CustomerAgent a, ShelfInstance s) => OnReachedShelf?.Invoke(a, s);
        public static void RaiseReachedCashier(PopLife.Customers.Runtime.CustomerAgent a, FacilityInstance f) => OnReachedCashier?.Invoke(a, f);
        public static void RaisePurchased(PopLife.Customers.Runtime.CustomerAgent a, ShelfInstance s, int qty, int price) => OnPurchased?.Invoke(a, s, qty, price);
        public static void RaiseCheckedOut(PopLife.Customers.Runtime.CustomerAgent a) => OnCheckedOut?.Invoke(a);
        public static void RaiseCustomerDestroyed(PopLife.Customers.Runtime.CustomerAgent a) => OnCustomerDestroyed?.Invoke(a);
    }
}