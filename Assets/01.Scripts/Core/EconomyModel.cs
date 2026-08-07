using System;

namespace CONFUSEDGAMEDEV.PollenGarden.Core
{
    /// <summary>
    /// The economy's whole state and rules: pollen in, pollen→nectar trade, nectar out. Pure C# —
    /// no engine types — so it runs identically on every platform and in EditMode tests. This is
    /// the first resident of the simulation core (Plan.md §3); offline progress will replay
    /// against this same class.
    /// </summary>
    public sealed class EconomyModel
    {
        /// <summary>Raised after any balance change. UI listens; nothing here renders.</summary>
        public event Action Changed;

        public int Pollen { get; private set; }

        public int Nectar { get; private set; }

        /// <summary>Trade rate: this much pollen buys one nectar. From EconomyConfig, never inline.</summary>
        public int PollenPerNectar { get; }

        public bool CanTradePollenForNectar => Pollen >= PollenPerNectar;

        public EconomyModel(int pollenPerNectar)
        {
            PollenPerNectar = Math.Max(pollenPerNectar, 1);
        }

        public void AddPollen(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Pollen += amount;
            Changed?.Invoke();
        }

        public void AddNectar(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Nectar += amount;
            Changed?.Invoke();
        }

        public bool TryTradePollenForNectar()
        {
            if (!CanTradePollenForNectar)
            {
                return false;
            }

            Pollen -= PollenPerNectar;
            Nectar += 1;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Load path: overwrite both balances wholesale (clamped at zero). Fires
        /// <see cref="Changed"/> once so the UI reflects the restored state.
        /// </summary>
        public void Restore(int pollen, int nectar)
        {
            Pollen = Math.Max(pollen, 0);
            Nectar = Math.Max(nectar, 0);
            Changed?.Invoke();
        }

        public bool TrySpendNectar(int cost)
        {
            if (cost <= 0 || Nectar < cost)
            {
                return false;
            }

            Nectar -= cost;
            Changed?.Invoke();
            return true;
        }
    }
}
