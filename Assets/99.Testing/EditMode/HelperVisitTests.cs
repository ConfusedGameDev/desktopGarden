using CONFUSEDGAMEDEV.PollenGarden.Core;
using CONFUSEDGAMEDEV.PollenGarden.Economy;
using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using CONFUSEDGAMEDEV.PollenGarden.Helpers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// The visit engine's two testable halves: the aggregate schedule math (N helpers on a
    /// T-second interval), and the harvest rules — pollen-type visits damage through the Damaged
    /// event, nectar-type visits damage silently and bank nectar (Plan.md §2 helper table).
    /// </summary>
    public sealed class HelperVisitTests
    {
        private GameObject root;
        private FlowerSpeciesData species;
        private HelperData helper;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            if (species != null)
            {
                Object.DestroyImmediate(species);
            }

            if (helper != null)
            {
                Object.DestroyImmediate(helper);
            }

            root = null;
            species = null;
            helper = null;
        }

        [Test]
        public void Accumulator_OneHelper_VisitsOncePerInterval()
        {
            var clock = new HelperVisitAccumulator(6.0);

            Assert.AreEqual(0, clock.Advance(5.9, 1));
            Assert.AreEqual(1, clock.Advance(0.1, 1), "Fractional progress must carry across calls.");
            Assert.AreEqual(1, clock.Advance(6.0, 1));
        }

        [Test]
        public void Accumulator_ThreeHelpers_TripleTheRate()
        {
            var clock = new HelperVisitAccumulator(6.0);

            Assert.AreEqual(3, clock.Advance(6.0, 3));
        }

        [Test]
        public void Accumulator_NoHelpersOrNoTime_NoVisits()
        {
            var clock = new HelperVisitAccumulator(6.0);

            Assert.AreEqual(0, clock.Advance(100.0, 0));
            Assert.AreEqual(0, clock.Advance(0.0, 5));
            Assert.AreEqual(0, clock.Advance(-1.0, 5));
        }

        private HelperManager BuildVisitScene(HelperYieldType yieldType, int damage)
        {
            species = ScriptableObject.CreateInstance<FlowerSpeciesData>();

            root = new GameObject("HelperVisitTest");
            var flower = root.AddComponent<FlowerController>();
            flower.Species = species;
            flower.Rebuild();

            var economy = root.AddComponent<EconomyManager>();
            var manager = root.AddComponent<HelperManager>();
            manager.Economy = economy;
            manager.TendedFlower = flower;

            helper = ScriptableObject.CreateInstance<HelperData>();
            var serialized = new SerializedObject(helper);
            serialized.FindProperty("yieldType").enumValueIndex = (int)yieldType;
            serialized.FindProperty("petalDamagePerVisit").intValue = damage;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return manager;
        }

        [Test]
        public void PollenVisit_DamagesThroughTheHarvestPath()
        {
            HelperManager manager = BuildVisitScene(HelperYieldType.PollenPerClick, 2);
            var flower = root.GetComponent<FlowerController>();
            PetalController petal = flower.Petals[0];
            int harvestEvents = 0;
            flower.PetalDamaged += _ => harvestEvents++;

            manager.ExecuteVisit(helper, petal);

            Assert.AreEqual(petal.MaxHitPoints - 2, petal.RemainingHitPoints);
            Assert.AreEqual(1, harvestEvents, "Pollen-type visit must ride the Damaged event.");
        }

        [Test]
        public void NectarVisit_DamagesSilentlyAndBanksNectar()
        {
            HelperManager manager = BuildVisitScene(HelperYieldType.NectarPerVisit, 4);
            var flower = root.GetComponent<FlowerController>();
            var economy = root.GetComponent<EconomyManager>();
            PetalController petal = flower.Petals[0];
            int harvestEvents = 0;
            flower.PetalDamaged += _ => harvestEvents++;

            manager.ExecuteVisit(helper, petal);

            Assert.AreEqual(petal.MaxHitPoints - 4, petal.RemainingHitPoints);
            Assert.AreEqual(1, economy.Model.Nectar);
            Assert.AreEqual(0, harvestEvents, "Nectar-type visit must not also pay pollen.");
        }

        [Test]
        public void Visit_WithDeadTarget_RetargetsALivingPetal()
        {
            HelperManager manager = BuildVisitScene(HelperYieldType.PollenPerClick, 2);
            var flower = root.GetComponent<FlowerController>();
            int before = flower.PetalCount * species.PetalHitPoints;

            manager.ExecuteVisit(helper, null);

            int after = 0;
            foreach (PetalController petal in flower.Petals)
            {
                after += petal.RemainingHitPoints;
            }

            Assert.AreEqual(before - 2, after, "The visit should land on some living petal.");
        }
    }
}
