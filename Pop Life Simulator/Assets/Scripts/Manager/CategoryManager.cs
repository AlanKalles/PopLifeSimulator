using UnityEngine;
using PopLife.Data;

namespace PopLife
{
    public class CategoryManager : MonoBehaviour
    {
        public static CategoryManager Instance;
        void Awake(){ Instance = this; }
        public float GetCategoryMultiplier(ProductCategory c) => 1f; // 原型：恒为1
    }
}
