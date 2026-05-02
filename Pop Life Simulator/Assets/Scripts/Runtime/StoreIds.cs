namespace PopLife.Runtime
{
    /// <summary>
    /// Stable store identifiers used as the lightweight ownership/routing protocol.
    /// </summary>
    public static class StoreIds
    {
        public const string PlayerStore = "PlayerStore";
        public const string Club = "Club";

        public static string ToId(StoreIdKind kind)
        {
            return kind == StoreIdKind.Club ? Club : PlayerStore;
        }
    }

    public enum StoreIdKind
    {
        PlayerStore,
        Club
    }
}
