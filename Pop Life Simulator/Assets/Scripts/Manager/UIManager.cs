using UnityEngine;

namespace PopLife
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;
        void Awake(){ Instance = this; }
        public void ShowMessage(string msg){ Debug.Log($"[UI] {msg}"); }
    }
}
