using CONFUSEDGAMEDEV.PollenGarden.Economy;
using CONFUSEDGAMEDEV.PollenGarden.Helpers;
using NUnit.Framework;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// The shop's one transaction: a purchase spends exactly the helper's cost and increments the
    /// ledger; an unaffordable purchase does neither.
    /// </summary>
    public sealed class HelperPurchaseTests
    {
        private GameObject root;
        private HelperData helper;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            if (helper != null)
            {
                Object.DestroyImmediate(helper);
            }

            root = null;
            helper = null;
        }

        [Test]
        public void Purchase_SpendsNectarAndIncrementsOwnership()
        {
            root = new GameObject("PurchaseTest");
            var economy = root.AddComponent<EconomyManager>();
            var helpers = root.AddComponent<HelperManager>();
            helpers.Economy = economy;
            helper = ScriptableObject.CreateInstance<HelperData>(); // default cost: 10

            economy.Model.AddNectar(15);

            Assert.IsTrue(helpers.CanAfford(helper));
            Assert.IsTrue(helpers.TryPurchase(helper));
            Assert.AreEqual(1, helpers.GetOwnedCount(helper));
            Assert.AreEqual(5, economy.Model.Nectar);

            Assert.IsFalse(helpers.CanAfford(helper));
            Assert.IsFalse(helpers.TryPurchase(helper));
            Assert.AreEqual(1, helpers.GetOwnedCount(helper));
            Assert.AreEqual(5, economy.Model.Nectar);
        }
    }
}
