using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PopLife.AlanBot.UI;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;

namespace PopLife.Tests.EditMode
{
    public class CustomerCodexPanelTests
    {
        [Test]
        public void UpdateXpProgressBar_WithZeroLoyaltyLevel_DoesNotThrow()
        {
            var panelObject = new GameObject("CustomerCodexPanel");
            var sliderObject = new GameObject("XpSlider");
            var textObject = new GameObject("XpText");
            CustomerArchetype archetype = null;

            try
            {
                var panel = panelObject.AddComponent<CustomerCodexPanel>();
                var slider = sliderObject.AddComponent<Slider>();
                var text = textObject.AddComponent<TextMeshProUGUI>();

                SetPrivateField(panel, "xpProgressBar", slider);
                SetPrivateField(panel, "xpProgressText", text);

                var record = new CustomerRecord
                {
                    loyaltyLevel = 0,
                    xp = 0
                };

                archetype = ScriptableObject.CreateInstance<CustomerArchetype>();
                archetype.levelUpThresholds = new[] { 100, 250, 500, 1000 };

                Assert.DoesNotThrow(() => InvokeUpdateXpProgressBar(panel, record, archetype));
                Assert.AreEqual(0f, slider.value);
                Assert.AreEqual("0/100", text.text);
            }
            finally
            {
                if (archetype != null)
                    Object.DestroyImmediate(archetype);
                Object.DestroyImmediate(textObject);
                Object.DestroyImmediate(sliderObject);
                Object.DestroyImmediate(panelObject);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = typeof(CustomerCodexPanel).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokeUpdateXpProgressBar(
            CustomerCodexPanel panel,
            CustomerRecord record,
            CustomerArchetype archetype)
        {
            var method = typeof(CustomerCodexPanel).GetMethod(
                "UpdateXpProgressBar",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method, "Missing UpdateXpProgressBar method");
            method.Invoke(panel, new object[] { record, archetype });
        }
    }
}
