using UnityEngine;

namespace PopLife
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;
        void Awake(){ Instance = this; }
        public void PlaySound(string key){ /* 原型期：不播，打印 */ Debug.Log($"[Audio] {key}"); }
    }
}
