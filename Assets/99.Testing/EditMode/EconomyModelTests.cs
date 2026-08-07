using CONFUSEDGAMEDEV.PollenGarden.Core;
using NUnit.Framework;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// The trade contract from Plan.md §2: pollen accumulates, 100 pollen buys exactly 1 nectar,
    /// nectar spends down and never below zero.
    /// </summary>
    public sealed class EconomyModelTests
    {
        private const int TradeRate = 100;

        [Test]
        public void AddPollen_Accumulates_AndIgnoresNonPositive()
        {
            var model = new EconomyModel(TradeRate);

            model.AddPollen(5);
            model.AddPollen(7);
            model.AddPollen(0);
            model.AddPollen(-3);

            Assert.AreEqual(12, model.Pollen);
        }

        [Test]
        public void Trade_100Pollen_Yields1Nectar()
        {
            var model = new EconomyModel(TradeRate);
            model.AddPollen(150);

            Assert.IsTrue(model.TryTradePollenForNectar());

            Assert.AreEqual(50, model.Pollen);
            Assert.AreEqual(1, model.Nectar);
        }

        [Test]
        public void Trade_WithInsufficientPollen_FailsWithoutSideEffects()
        {
            var model = new EconomyModel(TradeRate);
            model.AddPollen(99);

            Assert.IsFalse(model.CanTradePollenForNectar);
            Assert.IsFalse(model.TryTradePollenForNectar());
            Assert.AreEqual(99, model.Pollen);
            Assert.AreEqual(0, model.Nectar);
        }

        [Test]
        public void SpendNectar_FailsWhenShort_AndNeverGoesNegative()
        {
            var model = new EconomyModel(TradeRate);
            model.AddNectar(10);

            Assert.IsTrue(model.TrySpendNectar(10));
            Assert.AreEqual(0, model.Nectar);
            Assert.IsFalse(model.TrySpendNectar(1));
            Assert.AreEqual(0, model.Nectar);
        }

        [Test]
        public void Changed_FiresOncePerMutation_AndNotOnFailures()
        {
            var model = new EconomyModel(TradeRate);
            int changes = 0;
            model.Changed += () => changes++;

            model.AddPollen(100);          // 1
            model.TryTradePollenForNectar(); // 2
            model.TrySpendNectar(1);       // 3
            model.TrySpendNectar(1);       // failure — no event
            model.AddPollen(0);            // no-op — no event

            Assert.AreEqual(3, changes);
        }

        [Test]
        public void DegenerateTradeRate_IsClampedToOne()
        {
            var model = new EconomyModel(0);
            model.AddPollen(1);

            Assert.IsTrue(model.TryTradePollenForNectar());
            Assert.AreEqual(1, model.Nectar);
        }
    }
}
