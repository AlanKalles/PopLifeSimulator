using System;
using UnityEngine;

namespace PopLife.Title
{
    /// <summary>
    /// Title 场景启动器：实例化装饰顾客 + 播放标题 BGM
    /// 不引入 DayLoopManager / NavigationService / CommerceService 等运行时单例
    /// AudioManager 通过 ?. 安全访问，缺失时静默跳过——不 lazy 创建
    /// </summary>
    public class TitleSceneBootstrap : MonoBehaviour
    {
        [Serializable]
        public struct SpawnEntry
        {
            [Tooltip("生成位置（顾客实例化在此）")]
            public Transform spawnPoint;
            [Tooltip("该顾客循环走的 waypoint 列表")]
            public Transform[] waypoints;
        }

        [Header("顾客生成")]
        [SerializeField] private GameObject titleCustomerPrefab;
        [SerializeField] private SpawnEntry[] spawnEntries;
        [Tooltip("可选：若非空则随机分配给生成的顾客（覆盖 prefab 默认装扮）")]
        [SerializeField] private string[] customerIdPool;

        [Header("背景音乐")]
        [SerializeField] private string titleMusicKey = "BGM_Title";
        [SerializeField] private float musicFadeIn = 1f;

        private void Start()
        {
            SpawnCustomers();
            PlayMusic();
        }

        private void SpawnCustomers()
        {
            if (titleCustomerPrefab == null)
            {
                Debug.LogWarning("[TitleSceneBootstrap] titleCustomerPrefab 未配置，跳过顾客生成");
                return;
            }
            if (spawnEntries == null || spawnEntries.Length == 0) return;

            for (int i = 0; i < spawnEntries.Length; i++)
            {
                var entry = spawnEntries[i];
                if (entry.spawnPoint == null) continue;

                var go = Instantiate(titleCustomerPrefab, entry.spawnPoint.position, Quaternion.identity);

                // 随机外观
                if (customerIdPool != null && customerIdPool.Length > 0)
                {
                    var appearance = go.GetComponent<TitleCustomerAppearance>();
                    if (appearance != null)
                    {
                        string id = customerIdPool[UnityEngine.Random.Range(0, customerIdPool.Length)];
                        appearance.ApplyAppearance(id);
                    }
                }

                // 注入 waypoints
                var walker = go.GetComponent<TitleSceneCustomerWalker>();
                if (walker != null)
                    walker.SetWaypoints(entry.waypoints);
            }
        }

        private void PlayMusic()
        {
            if (string.IsNullOrEmpty(titleMusicKey)) return;
            // ?. 安全访问，AudioManager 缺失时静默跳过（不 lazy 创建）
            AudioManager.Instance?.PlayMusic(titleMusicKey, musicFadeIn);
        }
    }
}
