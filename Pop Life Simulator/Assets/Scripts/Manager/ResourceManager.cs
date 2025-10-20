using UnityEngine;

namespace PopLife
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance;
        public int money, fame;

        void Awake()
        {
            Instance = this;
        }

        public int GetFame() { return fame; }
        public int GetMoney() { return money; }

        /// <summary>
        /// 检查玩家是否有足够的资源
        /// </summary>
        public bool CanAfford(int moneyCost, int fameCost)
        {
            return money >= moneyCost && fame >= fameCost;
        }

        public void Spend(int moneyCost, int fameCost)
        {
            money -= moneyCost;
            fame -= fameCost;
        }

        public void SpendMoney(int m)
        {
            money -= m;
        }

        public void AddMoney(int m)
        {
            money += m;
        }

        public void AddFame(int f)
        {
            fame += f;
        }
    }
}
