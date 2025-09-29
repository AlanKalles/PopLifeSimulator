using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;


namespace PopLife.Customers.Spawner
{
    public class CustomerSpawner : MonoBehaviour
    {
        [Header("基础配置")]
        public GameObject customerPrefab; // 带 CustomerAgent + CustomerBlackboardAdapter
        public Transform spawnPoint;
        [Tooltip("与 ProductCategory 对齐的品类数量")]
        public int categoriesCount = 6;

        [Header("默认配置 (当找不到顾客记录时使用)")]
        public CustomerArchetype defaultArchetype;
        public Trait[] defaultTraits;

        [Header("手动生成指定顾客")]
        [Tooltip("要生成的顾客ID")]
        public string targetCustomerId;

        [Tooltip("点击以生成指定ID的顾客")]
        public bool spawnTargetCustomer = false;

        [Header("调试信息")]
        [SerializeField] private int loadedCustomerCount = 0;
        [SerializeField] private string lastSpawnedCustomer = "";

        private CustomerRepository repository;
        private bool repositoryLoaded = false;

        void Awake()
        {
            repository = CustomerRepository.Instance;
            if (repository == null)
            {
                repository = FindObjectOfType<CustomerRepository>();
            }

            if (repository == null)
            {
                Debug.LogError("CustomerSpawner: 找不到 CustomerRepository！");
                return;
            }
        }

        void Start()
        {
            LoadCustomerData();
        }

        void Update()
        {
            if (spawnTargetCustomer)
            {
                spawnTargetCustomer = false;
                SpawnCustomerById(targetCustomerId);
            }
        }

        void LoadCustomerData()
        {
            if (repository == null) return;

            repository.Load();
            repositoryLoaded = true;

            var allCustomers = repository.All().ToList();
            loadedCustomerCount = allCustomers.Count;

            Debug.Log($"CustomerSpawner: 成功加载 {loadedCustomerCount} 个顾客记录");

            if (loadedCustomerCount > 0)
            {
                Debug.Log("已加载的顾客ID列表:");
                foreach (var customer in allCustomers)
                {
                    Debug.Log($"  - {customer.customerId}: {customer.name}");
                }
            }
        }

        public void SpawnCustomerById(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                Debug.LogWarning("CustomerSpawner: 请输入要生成的顾客ID!");
                return;
            }

            if (!repositoryLoaded)
            {
                Debug.LogWarning("CustomerSpawner: 正在加载顾客数据，请稍后...");
                LoadCustomerData();
                return;
            }

            if (customerPrefab == null)
            {
                Debug.LogError("CustomerSpawner: 缺少顾客预制体!");
                return;
            }

            if (spawnPoint == null)
            {
                Debug.LogWarning("CustomerSpawner: 未设置生成点，将在原点生成");
            }

            CustomerRecord record = repository.Get(customerId);

            if (record == null)
            {
                Debug.LogError($"CustomerSpawner: 找不到ID为 '{customerId}' 的顾客记录!");
                Debug.Log($"当前已加载 {loadedCustomerCount} 个顾客，请检查ID是否正确");
                return;
            }

            // 尝试从记录中获取原型
            CustomerArchetype archetype = null;

            if (!string.IsNullOrEmpty(record.archetypeId))
            {
                // 从 Resources 文件夹加载原型
                archetype = Resources.Load<CustomerArchetype>($"ScriptableObjects/CustomerArchetypes/{record.archetypeId}");

                if (archetype == null)
                {
                    Debug.LogWarning($"CustomerSpawner: 找不到原型 '{record.archetypeId}'，将使用默认原型");
                }
            }

            // 如果没找到，使用默认原型
            if (archetype == null)
            {
                archetype = defaultArchetype;
            }

            // 如果还是没有原型，报错并返回
            if (archetype == null)
            {
                Debug.LogError($"CustomerSpawner: 无法为顾客 '{record.name}' (ID: {record.customerId}) 找到任何可用的原型！");
                Debug.LogError("请确保：1) 设置了默认原型，或 2) 顾客记录有有效的 archetypeId");
                return;
            }

            // 尝试从记录中获取特质
            List<Trait> traitsList = new List<Trait>();

            if (record.traitIds != null && record.traitIds.Length > 0)
            {
                foreach (string traitId in record.traitIds)
                {
                    if (string.IsNullOrEmpty(traitId)) continue;

                    // 从 Resources 文件夹加载特质
                    Trait trait = Resources.Load<Trait>($"ScriptableObjects/Traits/{traitId}");

                    if (trait != null)
                    {
                        traitsList.Add(trait);
                    }
                    else
                    {
                        Debug.LogWarning($"CustomerSpawner: 找不到特质 '{traitId}'");
                    }
                }
            }

            // 如果没有找到任何特质，使用默认特质
            Trait[] traits = traitsList.Count > 0 ? traitsList.ToArray() : (defaultTraits ?? new Trait[0]);

            int daySeed = UnityEngine.Random.Range(0, int.MaxValue);

            Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            GameObject go = Instantiate(customerPrefab, spawnPosition, Quaternion.identity);

            CustomerAgent agent = go.GetComponent<CustomerAgent>();
            if (agent == null)
            {
                Debug.LogError("CustomerSpawner: 顾客预制体缺少 CustomerAgent 组件!");
                Destroy(go);
                return;
            }

            agent.Initialize(record, archetype, traits, categoriesCount, daySeed);

            lastSpawnedCustomer = $"{record.customerId}: {record.name}";
            Debug.Log($"成功生成顾客: {lastSpawnedCustomer}");
        }

        [ContextMenu("重新加载顾客数据")]
        public void ReloadCustomerData()
        {
            LoadCustomerData();
        }

        [ContextMenu("生成目标顾客")]
        public void SpawnTargetCustomerFromMenu()
        {
            SpawnCustomerById(targetCustomerId);
        }
    }
}