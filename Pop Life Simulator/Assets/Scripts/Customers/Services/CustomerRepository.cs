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


        public CustomerRecord Get(string id) => _byId.TryGetValue(id, out var r) ? r : null;
        public void Put(CustomerRecord r){ _byId[r.customerId] = r; }
        public IEnumerable<CustomerRecord> All() { return _byId.Values; }


        public void Load()
        {
            _byId.Clear();
            var path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var list = JsonUtility.FromJson<CustomerRecordList>(json);
            if (list?.items != null)
                foreach (var r in list.items)
                    _byId[r.customerId] = r;
        }
        public void Save()
        {
            var list = new CustomerRecordList();
            list.items.AddRange(_byId.Values);
            var json = JsonUtility.ToJson(list, true);
            var path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, json);
        }
    }
}