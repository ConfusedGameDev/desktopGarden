using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Marker on the generated centre disc that turns a raycast hit into the flower's
    /// <see cref="FlowerController.CenterClicked"/> event — the "flower button" that opens the
    /// expanded menu. Owns nothing; regenerated with the disc on every rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowerCenterButton : MonoBehaviour
    {
        private FlowerController flower;

        public void Initialize(FlowerController owner)
        {
            flower = owner;
        }

        public void Click()
        {
            if (flower != null)
            {
                flower.NotifyCenterClicked();
            }
        }
    }
}
