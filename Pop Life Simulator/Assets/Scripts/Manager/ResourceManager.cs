using UnityEngine;

namespace PopLife
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance;
        public int money, fame; // 仅为接口占位
        void Awake(){ Instance = this; }

        public bool CanAfford(int moneyCost, int fameCost) => true; // 一律通过
        public void Spend(int moneyCost, int fameCost){ money -= moneyCost; fame -= fameCost; }
        public void SpendMoney(int m){ money -= m; }
        public void AddMoney(int m){ money += m; }
        public void AddFame(int f){ fame += f; }
    }
}
