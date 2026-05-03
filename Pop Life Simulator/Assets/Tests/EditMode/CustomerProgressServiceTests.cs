using NUnit.Framework;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;

namespace PopLife.Tests.EditMode
{
    public class CustomerProgressServiceTests
    {
        [Test]
        public void CalculateLevel_UsesOneBasedLoyaltyLevels()
        {
            int[] thresholds = { 100, 250, 500, 1000 };

            Assert.AreEqual(1, CustomerProgressService.CalculateLevel(0, thresholds));
            Assert.AreEqual(1, CustomerProgressService.CalculateLevel(99, thresholds));
            Assert.AreEqual(2, CustomerProgressService.CalculateLevel(100, thresholds));
            Assert.AreEqual(3, CustomerProgressService.CalculateLevel(250, thresholds));
            Assert.AreEqual(4, CustomerProgressService.CalculateLevel(500, thresholds));
            Assert.AreEqual(5, CustomerProgressService.CalculateLevel(1000, thresholds));
        }

        [Test]
        public void CalculateLevel_WithMissingThresholds_ReturnsDefaultLoyaltyLevel()
        {
            Assert.AreEqual(1, CustomerProgressService.CalculateLevel(0, null));
            Assert.AreEqual(1, CustomerProgressService.CalculateLevel(1000, new int[0]));
        }

        [Test]
        public void ApplySessionRewards_NormalizesStoredZeroLoyaltyLevel()
        {
            var serviceObject = new GameObject("CustomerProgressService");
            CustomerArchetype archetype = null;

            try
            {
                var service = serviceObject.AddComponent<CustomerProgressService>();
                var record = new CustomerRecord
                {
                    loyaltyLevel = 0,
                    xp = 0
                };
                var session = new CustomerSession
                {
                    moneySpent = 0
                };
                archetype = ScriptableObject.CreateInstance<CustomerArchetype>();
                archetype.levelUpThresholds = new[] { 100, 250, 500, 1000 };

                service.ApplySessionRewards(record, session, archetype);

                Assert.AreEqual(1, record.loyaltyLevel);
                Assert.AreEqual(0, record.xp);
            }
            finally
            {
                if (archetype != null)
                    Object.DestroyImmediate(archetype);
                Object.DestroyImmediate(serviceObject);
            }
        }
    }
}
