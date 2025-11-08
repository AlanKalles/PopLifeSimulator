using UnityEngine;
using UnityEditor;
using PopLife;
using PopLife.Runtime;
using PopLife.Customers.Runtime;
using PopLife.Customers.Spawner;

namespace PopLife.Editor
{
    /// <summary>
    /// 商店入口系统诊断工具
    /// 用于检查入口配置是否正确
    /// </summary>
    public class EntranceDiagnostic : EditorWindow
    {
        [MenuItem("PopLife/Diagnostics/Entrance System Check")]
        public static void ShowWindow()
        {
            GetWindow<EntranceDiagnostic>("Entrance Diagnostic");
        }

        private Vector2 scrollPosition;

        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("Entrance System Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Run Full Check", GUILayout.Height(30)))
            {
                RunFullCheck();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Manual Checks:", EditorStyles.boldLabel);

            if (GUILayout.Button("Check EntranceManager"))
            {
                CheckEntranceManager();
            }

            if (GUILayout.Button("Check StoreEntrancePoints"))
            {
                CheckStoreEntrancePoints();
            }

            if (GUILayout.Button("Check CustomerSpawner"))
            {
                CheckCustomerSpawner();
            }

            if (GUILayout.Button("Check Active Customers"))
            {
                CheckActiveCustomers();
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunFullCheck()
        {
            Debug.Log("========== ENTRANCE SYSTEM FULL CHECK ==========");
            CheckEntranceManager();
            CheckStoreEntrancePoints();
            CheckCustomerSpawner();
            CheckActiveCustomers();
            Debug.Log("========== CHECK COMPLETE ==========");
        }

        private void CheckEntranceManager()
        {
            Debug.Log("--- Checking EntranceManager ---");

            var manager = FindFirstObjectByType<EntranceManager>();
            if (manager == null)
            {
                Debug.LogError("❌ EntranceManager not found in scene!");
                Debug.LogError("解决方案: 在场景中创建一个 GameObject 并添加 EntranceManager 组件");
                return;
            }

            Debug.Log($"✅ EntranceManager found: {manager.gameObject.name}");
            Debug.Log($"   - autoDiscoverEntrances: {manager.autoDiscoverEntrances}");
            Debug.Log($"   - entrances count: {manager.entrances.Count}");

            if (manager.entrances.Count == 0)
            {
                Debug.LogWarning("⚠️ EntranceManager has no entrances registered!");
                Debug.LogWarning("解决方案: 确保场景中有 StoreEntrancePoint 且 EntranceManager.autoDiscoverEntrances = true");
            }
            else
            {
                foreach (var entrance in manager.entrances)
                {
                    if (entrance != null)
                    {
                        Debug.Log($"   - Entrance: {entrance.entranceId} (isMain: {entrance.isMainEntrance})");
                    }
                }
            }

            var mainEntrance = manager.GetMainEntrance();
            if (mainEntrance == null)
            {
                Debug.LogError("❌ No main entrance found!");
                Debug.LogError("解决方案: 确保至少有一个 StoreEntrancePoint 的 isMainEntrance = true");
            }
            else
            {
                Debug.Log($"✅ Main entrance: {mainEntrance.entranceId}");
            }
        }

        private void CheckStoreEntrancePoints()
        {
            Debug.Log("--- Checking StoreEntrancePoints ---");

            var entrancePoints = FindObjectsByType<StoreEntrancePoint>(FindObjectsSortMode.None);

            if (entrancePoints.Length == 0)
            {
                Debug.LogError("❌ No StoreEntrancePoint found in scene!");
                Debug.LogError("解决方案: ");
                Debug.LogError("1. 创建一个 GameObject (如 'MainEntrance')");
                Debug.LogError("2. 添加 StoreEntrancePoint 组件");
                Debug.LogError("3. 配置 NodeLink2 连接外部和内部锚点");
                return;
            }

            Debug.Log($"✅ Found {entrancePoints.Length} StoreEntrancePoint(s)");

            foreach (var entrance in entrancePoints)
            {
                Debug.Log($"\n   Entrance: {entrance.entranceId}");
                Debug.Log($"   - GameObject: {entrance.gameObject.name}");
                Debug.Log($"   - isMainEntrance: {entrance.isMainEntrance}");

                if (entrance.nodeLink == null)
                {
                    Debug.LogError($"   ❌ NodeLink2 is null!");
                    Debug.LogError($"   解决方案: 为 {entrance.gameObject.name} 的 StoreEntrancePoint.nodeLink 分配 NodeLink2 组件");
                }
                else
                {
                    Debug.Log($"   ✅ NodeLink2 assigned");
                }

                if (entrance.outsideAnchor == null)
                {
                    Debug.LogWarning($"   ⚠️ outsideAnchor is null (should be auto-set in Awake)");
                }
                else
                {
                    Debug.Log($"   ✅ outsideAnchor: {entrance.outsideAnchor.position}");
                }

                if (entrance.insideAnchor == null)
                {
                    Debug.LogWarning($"   ⚠️ insideAnchor is null (should be auto-set in Awake)");
                }
                else
                {
                    Debug.Log($"   ✅ insideAnchor: {entrance.insideAnchor.position}");
                }
            }
        }

        private void CheckCustomerSpawner()
        {
            Debug.Log("--- Checking CustomerSpawner ---");

            var spawner = FindFirstObjectByType<CustomerSpawner>();
            if (spawner == null)
            {
                Debug.LogError("❌ CustomerSpawner not found in scene!");
                return;
            }

            Debug.Log($"✅ CustomerSpawner found: {spawner.gameObject.name}");
            Debug.Log($"   - customerPrefab: {(spawner.customerPrefab != null ? spawner.customerPrefab.name : "NULL")}");
            Debug.Log($"   - spawnPoints count: {(spawner.spawnPoints != null ? spawner.spawnPoints.Length : 0)}");

            if (spawner.spawnPoints != null && spawner.spawnPoints.Length > 0)
            {
                foreach (var sp in spawner.spawnPoints)
                {
                    if (sp != null)
                    {
                        Debug.Log($"   - SpawnPoint: {sp.name} at {sp.position}");
                    }
                }
            }
        }

        private void CheckActiveCustomers()
        {
            Debug.Log("--- Checking Active Customers ---");

            var customers = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);

            if (customers.Length == 0)
            {
                Debug.Log("   No active customers in scene");
                return;
            }

            Debug.Log($"✅ Found {customers.Length} active customer(s)");

            foreach (var customer in customers)
            {
                var bb = customer.GetComponent<CustomerBlackboardAdapter>();
                if (bb == null)
                {
                    Debug.LogWarning($"   ⚠️ Customer {customer.gameObject.name} has no CustomerBlackboardAdapter");
                    continue;
                }

                Debug.Log($"\n   Customer: {bb.customerId}");
                Debug.Log($"   - GameObject: {customer.gameObject.name}");
                Debug.Log($"   - Position: {customer.transform.position}");

                if (bb.entranceOutsideAnchor == null)
                {
                    Debug.LogError($"   ❌ entranceOutsideAnchor is NULL!");
                    Debug.LogError($"   这会导致 MoveToEntranceAction 失败");
                    Debug.LogError($"   解决方案: 检查 CustomerSpawner.SpawnCustomer() 中的入口设置逻辑");
                }
                else
                {
                    Debug.Log($"   ✅ entranceOutsideAnchor: {bb.entranceOutsideAnchor.position}");
                }

                if (bb.entranceInsideAnchor == null)
                {
                    Debug.LogError($"   ❌ entranceInsideAnchor is NULL!");
                }
                else
                {
                    Debug.Log($"   ✅ entranceInsideAnchor: {bb.entranceInsideAnchor.position}");
                }

                Debug.Log($"   - hasEnteredStore: {bb.hasEnteredStore}");
                Debug.Log($"   - spawnPoint: {(bb.spawnPoint != null ? bb.spawnPoint.position.ToString() : "NULL")}");
            }
        }
    }
}
