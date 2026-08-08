using CONFUSEDGAMEDEV.PollenGarden.Core;
using NUnit.Framework;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// The edge-picking rule behind helper spawns: a flower near a corner gets exactly that
    /// corner's two edges.
    /// </summary>
    public sealed class ScreenEdgeTests
    {
        [Test]
        public void BottomRightCorner_PicksRightAndBottom()
        {
            (ScreenEdge horizontal, ScreenEdge vertical) = ScreenEdges.ClosestTwo(0.85, 0.2);

            Assert.AreEqual(ScreenEdge.Right, horizontal);
            Assert.AreEqual(ScreenEdge.Bottom, vertical);
        }

        [Test]
        public void TopLeftCorner_PicksLeftAndTop()
        {
            (ScreenEdge horizontal, ScreenEdge vertical) = ScreenEdges.ClosestTwo(0.1, 0.9);

            Assert.AreEqual(ScreenEdge.Left, horizontal);
            Assert.AreEqual(ScreenEdge.Top, vertical);
        }

        [Test]
        public void DeadCentre_StillYieldsOneEdgePerAxis()
        {
            (ScreenEdge horizontal, ScreenEdge vertical) = ScreenEdges.ClosestTwo(0.5, 0.5);

            Assert.IsTrue(horizontal == ScreenEdge.Left || horizontal == ScreenEdge.Right);
            Assert.IsTrue(vertical == ScreenEdge.Bottom || vertical == ScreenEdge.Top);
        }
    }
}
