using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;


namespace PopLife.Customers.Spawner
{
    public class CustomerSpawner : MonoBehaviour
    {
        public SpawnerProfile profile;
        public Transform spawnPoint;
        public GameObject customerPrefab; // 带 CustomerAgent + CustomerBlackboardAdapter
        [Tooltip("与 ProductCategory 对齐的品类数量")] public int categoriesCount = 5;


        public void SpawnOne(CustomerRecord record, CustomerArchetype arch, Trait[] traits, int daySeed)
        {
            var go = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
            var agent = go.GetComponent<CustomerAgent>();
            agent.Initialize(record, arch, traits, categoriesCount, daySeed);
        }
    }
}