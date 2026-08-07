using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using NUnit.Framework;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// Geometry contract for <see cref="PetalMeshBuilder"/>. Everything here is deterministic and
    /// scene-free, which is exactly why it belongs in EditMode.
    /// </summary>
    public sealed class PetalMeshBuilderTests
    {
        private const float Tolerance = 1e-4f;

        private Mesh mesh;

        [TearDown]
        public void TearDown()
        {
            if (mesh != null)
            {
                Object.DestroyImmediate(mesh);
            }

            mesh = null;
        }

        [Test]
        public void Petal_DefaultShape_Has25VerticesAnd23Triangles()
        {
            PetalShapeParameters shape = PetalShapeParameters.Default;
            mesh = PetalMeshBuilder.CreatePetal(shape);

            Assert.AreEqual(12, shape.LengthSegments, "Default segment count changed; update the expectations below.");
            Assert.AreEqual(25, mesh.vertexCount);
            Assert.AreEqual(23, mesh.triangles.Length / 3);
            Assert.AreEqual(shape.VertexCount, mesh.vertexCount);
            Assert.AreEqual(shape.TriangleCount, mesh.triangles.Length / 3);
        }

        [Test]
        public void Petal_AllNormalsFaceNegativeZ()
        {
            mesh = PetalMeshBuilder.CreatePetal(PetalShapeParameters.Default);

            foreach (Vector3 normal in mesh.normals)
            {
                Assert.AreEqual(0f, normal.x, Tolerance);
                Assert.AreEqual(0f, normal.y, Tolerance);
                Assert.AreEqual(-1f, normal.z, Tolerance);
            }
        }

        [Test]
        public void Petal_AllVerticesAreCoplanarAtZeroZ()
        {
            mesh = PetalMeshBuilder.CreatePetal(PetalShapeParameters.Default);

            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.AreEqual(0f, vertex.z, Tolerance);
            }
        }

        [Test]
        public void Petal_BaseSitsAtOrigin()
        {
            // Load-bearing: the petal's hinge must be its own base so M1's droop and sway rotate
            // about the right point. A centre-of-flower pivot would break both.
            PetalShapeParameters shape = PetalShapeParameters.Default;
            mesh = PetalMeshBuilder.CreatePetal(shape);
            Vector3[] vertices = mesh.vertices;

            Assert.AreEqual(0f, vertices[0].x, Tolerance);
            Assert.AreEqual(0f, vertices[0].y, Tolerance);
            Assert.AreEqual(0f, vertices[1].x, Tolerance);
            Assert.AreEqual(0f, vertices[1].y, Tolerance);
        }

        [Test]
        public void Petal_TipIsSingleVertexAtLength()
        {
            PetalShapeParameters shape = PetalShapeParameters.Default;
            mesh = PetalMeshBuilder.CreatePetal(shape);
            Vector3[] vertices = mesh.vertices;

            Vector3 tip = vertices[vertices.Length - 1];
            Assert.AreEqual(0f, tip.x, Tolerance);
            Assert.AreEqual(shape.Length, tip.y, Tolerance);

            for (int i = 0; i < vertices.Length - 1; i++)
            {
                Assert.Less(vertices[i].y, shape.Length - Tolerance,
                    $"Vertex {i} reaches the tip plane; the tip should be the only vertex there.");
            }
        }

        [Test]
        public void Petal_WidestPointMatchesAnalyticPeak()
        {
            PetalShapeParameters shape = PetalShapeParameters.Default;

            float peakT = shape.WidestPointT;
            Assert.AreEqual(0.8f / (0.8f + 0.6f), peakT, Tolerance);
            Assert.AreEqual(shape.HalfWidth, shape.HalfWidthAt(peakT), Tolerance);

            // The analytic peak really is the maximum across the whole petal.
            for (int i = 0; i <= 200; i++)
            {
                float t = i / 200f;
                Assert.LessOrEqual(shape.HalfWidthAt(t), shape.HalfWidth + Tolerance);
            }
        }

        [Test]
        public void Petal_EndsHaveZeroWidth()
        {
            PetalShapeParameters shape = PetalShapeParameters.Default;

            Assert.AreEqual(0f, shape.HalfWidthAt(0f), Tolerance);
            Assert.AreEqual(0f, shape.HalfWidthAt(1f), Tolerance);
        }

        [Test]
        public void Petal_AllTrianglesWindTowardNegativeZ()
        {
            mesh = PetalMeshBuilder.CreatePetal(PetalShapeParameters.Default);
            AssertWindingFacesNegativeZ(mesh, expectedDegenerateCount: 1);
        }

        [Test]
        public void CenterDisc_16Segments_Has17VerticesAnd16Triangles()
        {
            mesh = PetalMeshBuilder.CreateCenterDisc(0.42f, 16);

            Assert.AreEqual(17, mesh.vertexCount);
            Assert.AreEqual(16, mesh.triangles.Length / 3);
        }

        [Test]
        public void CenterDisc_AllTrianglesWindTowardNegativeZ()
        {
            mesh = PetalMeshBuilder.CreateCenterDisc(0.42f, 16);
            AssertWindingFacesNegativeZ(mesh, expectedDegenerateCount: 0);
        }

        [Test]
        public void CenterDisc_RimSitsOnRadius()
        {
            const float radius = 0.42f;
            mesh = PetalMeshBuilder.CreateCenterDisc(radius, 16);
            Vector3[] vertices = mesh.vertices;

            Assert.AreEqual(Vector3.zero, vertices[0]);

            for (int i = 1; i < vertices.Length; i++)
            {
                Assert.AreEqual(radius, new Vector2(vertices[i].x, vertices[i].y).magnitude, Tolerance);
            }
        }

        [Test]
        public void WritePetal_IsIdempotent_AndReusesTheSameMesh()
        {
            mesh = PetalMeshBuilder.CreatePetal(PetalShapeParameters.Default);
            int vertexCount = mesh.vertexCount;
            int triangleCount = mesh.triangles.Length;

            PetalMeshBuilder.WritePetal(mesh, PetalShapeParameters.Default);
            PetalMeshBuilder.WritePetal(mesh, PetalShapeParameters.Default);

            Assert.AreEqual(vertexCount, mesh.vertexCount);
            Assert.AreEqual(triangleCount, mesh.triangles.Length);
        }

        [Test]
        public void Petal_DegenerateShapeParameters_AreClampedNotCrashed()
        {
            PetalShapeParameters shape = new PetalShapeParameters(0f, 0f, 0, 0f, 0f);
            mesh = PetalMeshBuilder.CreatePetal(shape);

            Assert.AreEqual(PetalShapeParameters.MinLengthSegments, shape.Clamped().LengthSegments);
            Assert.Greater(mesh.vertexCount, 0);
        }

        private static void AssertWindingFacesNegativeZ(Mesh target, int expectedDegenerateCount)
        {
            Vector3[] vertices = target.vertices;
            int[] triangles = target.triangles;
            int degenerateCount = 0;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];

                float crossZ = Vector3.Cross(b - a, c - a).z;

                if (Mathf.Abs(crossZ) <= Tolerance)
                {
                    degenerateCount++;
                    continue;
                }

                Assert.Less(crossZ, 0f,
                    $"Triangle {i / 3} winds toward +Z; it would be back-facing to the camera.");
            }

            Assert.AreEqual(expectedDegenerateCount, degenerateCount,
                "Unexpected number of degenerate triangles.");
        }
    }
}
