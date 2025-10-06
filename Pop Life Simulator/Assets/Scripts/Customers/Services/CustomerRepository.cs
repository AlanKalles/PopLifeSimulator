using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Utility;


namespace PopLife.Customers.Services
{
    [Serializable]
    internal class CustomerRecordList { public List<CustomerRecord> items = new(); }

    [DefaultExecutionOrder(-50)]
    public class CustomerRepository : MonoBehaviour
    {
        public static CustomerRepository Instance;
        void Awake(){ Instance = this; }

        void Start()
        {
            Load(); // 启动时自动加载顾客数据
        }

        private readonly Dictionary<string, CustomerRecord> _byId = new();
        [SerializeField] private string fileName = "Customers.json";
        private string FilePath => SavePathManager.GetReadPath(fileName);
        private string SavePath => SavePathManager.GetWritePath(fileName);


        public CustomerRecord Get(string id) => _byId.TryGetValue(id, out var r) ? r : null;
        public void Put(CustomerRecord r){ _byId[r.customerId] = r; }
        public IEnumerable<CustomerRecord> All() { return _byId.Values; }


        public void Load()
        {
            _byId.Clear();

            string path = FilePath;

            // 确保目录存在
            SavePathManager.EnsureDirectoryExists(path);

            if (!File.Exists(path))
            {
                Debug.Log($"Customers.json not found at: {path}");
                return;
            }

            var json = File.ReadAllText(path);
            var list = JsonUtility.FromJson<CustomerRecordList>(json);
            if (list?.items != null)
            {
                foreach (var r in list.items)
                    _byId[r.customerId] = r;
                Debug.Log($"Loaded {list.items.Count} customer records from {path}");
            }
        }
        public void Save()
        {
            string path = SavePath;

            // 确保目录存在
            SavePathManager.EnsureDirectoryExists(path);

            var list = new CustomerRecordList();
            list.items.AddRange(_byId.Values);
            var json = JsonUtility.ToJson(list, true);
            File.WriteAllText(path, json);
            Debug.Log($"Saved {list.items.Count} customer records to {path}");
        }
    }
}