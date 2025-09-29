using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PopLife.Customers.Runtime;


namespace PopLife.Customers.Services
{
    [Serializable]
    internal class CustomerRecordList { public List<CustomerRecord> items = new(); }
    public class CustomerRepository : MonoBehaviour
    {
        public static CustomerRepository Instance;
        void Awake(){ Instance = this; }


        private readonly Dictionary<string, CustomerRecord> _byId = new();
        [SerializeField] private string fileName = "Customers.json";
        private string SaveFolderPath => Path.Combine(Application.dataPath, "Documents", "Save");
        private string FilePath => Path.Combine(SaveFolderPath, fileName);


        public CustomerRecord Get(string id) => _byId.TryGetValue(id, out var r) ? r : null;
        public void Put(CustomerRecord r){ _byId[r.customerId] = r; }
        public IEnumerable<CustomerRecord> All() { return _byId.Values; }


        public void Load()
        {
            _byId.Clear();

            // 确保文件夹存在
            if (!Directory.Exists(SaveFolderPath))
            {
                Directory.CreateDirectory(SaveFolderPath);
                Debug.Log($"Created save directory: {SaveFolderPath}");
            }

            if (!File.Exists(FilePath))
            {
                Debug.Log($"Customers.json not found at: {FilePath}");
                return;
            }

            var json = File.ReadAllText(FilePath);
            var list = JsonUtility.FromJson<CustomerRecordList>(json);
            if (list?.items != null)
            {
                foreach (var r in list.items)
                    _byId[r.customerId] = r;
                Debug.Log($"Loaded {list.items.Count} customer records from {FilePath}");
            }
        }
        public void Save()
        {
            // 确保文件夹存在
            if (!Directory.Exists(SaveFolderPath))
            {
                Directory.CreateDirectory(SaveFolderPath);
                Debug.Log($"Created save directory: {SaveFolderPath}");
            }

            var list = new CustomerRecordList();
            list.items.AddRange(_byId.Values);
            var json = JsonUtility.ToJson(list, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"Saved {list.items.Count} customer records to {FilePath}");
        }
    }
}