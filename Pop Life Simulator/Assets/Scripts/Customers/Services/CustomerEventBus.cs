using System;


namespace PopLife.Customers.Services
{
    public static class CustomerEventBus
    {
        public static event Action<PopLife.Customers.Runtime.CustomerAgent> OnSpawned;
        public static event Action<string> OnBuildingChanged; // buildingId
        public static event Action<string> OnShelfSoldOut; // shelfId
        public static event Action OnEEBChanged;


        public static void RaiseSpawned(PopLife.Customers.Runtime.CustomerAgent a) => OnSpawned?.Invoke(a);
        public static void RaiseBuildingChanged(string id) => OnBuildingChanged?.Invoke(id);
        public static void RaiseShelfSoldOut(string id) => OnShelfSoldOut?.Invoke(id);
        public static void RaiseEEBChanged() => OnEEBChanged?.Invoke();
    }
}