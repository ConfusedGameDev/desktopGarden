using System;
using System.Collections.Generic;
using CONFUSEDGAMEDEV.PollenGarden.Economy;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Helpers
{
    /// <summary>
    /// Ownership ledger and purchase path for helpers. M2 grows this into visit scheduling,
    /// pooling and delegation; today it owns exactly one fact — how many of each helper the
    /// player has bought — and guards the nectar transaction.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Helper Manager")]
    public sealed class HelperManager : MonoBehaviour
    {
        [SerializeField]
        private EconomyManager economy;

        [Tooltip("What the shop offers, in display order.")]
        [SerializeField]
        private List<HelperData> availableHelpers = new List<HelperData>();

        private readonly Dictionary<HelperData, int> ownedCounts = new Dictionary<HelperData, int>();

        /// <summary>Raised when a purchase changes the ownership ledger.</summary>
        public event Action Changed;

        public EconomyManager Economy
        {
            get => economy;
            set => economy = value;
        }

        public List<HelperData> AvailableHelpers => availableHelpers;

        /// <summary>Snapshot for the save system; only owned helpers appear.</summary>
        public IReadOnlyDictionary<HelperData, int> OwnedCounts => ownedCounts;

        public int GetOwnedCount(HelperData helper)
        {
            return helper != null && ownedCounts.TryGetValue(helper, out int count) ? count : 0;
        }

        public bool CanAfford(HelperData helper)
        {
            return helper != null && economy != null && economy.Model.Nectar >= helper.NectarCost;
        }

        /// <summary>Load path: set an owned count directly — no nectar changes hands.</summary>
        public void RestoreOwnedCount(HelperData helper, int count)
        {
            if (helper == null)
            {
                return;
            }

            if (count <= 0)
            {
                ownedCounts.Remove(helper);
            }
            else
            {
                ownedCounts[helper] = count;
            }

            Changed?.Invoke();
        }

        public bool TryPurchase(HelperData helper)
        {
            if (helper == null || economy == null || !economy.Model.TrySpendNectar(helper.NectarCost))
            {
                return false;
            }

            ownedCounts[helper] = GetOwnedCount(helper) + 1;
            Changed?.Invoke();
            return true;
        }
    }
}
