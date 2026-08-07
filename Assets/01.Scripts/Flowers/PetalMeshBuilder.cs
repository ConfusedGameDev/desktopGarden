using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Generates the flat, stylized petal and centre-disc meshes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Meshes are authored in the <b>XY plane at z = 0, base at the origin, growing along +Y</b>,
    /// facing <b>-Z</b> (toward a camera on the -Z side, matching Unity's built-in Quad convention).
    /// </para>
    /// <para>
    /// <b>Do not "simplify" the petal to be centre-of-flower pivoted.</b> Base-at-origin puts each
    /// petal's hinge at its own base, which is what the ambient sway (rotate about local Z) and the
    /// HP droop (rotate about local X) need. A centre-pivoted mesh would swing petals through the
    /// disc. Radial placement is the caller's job, via the petal transform.
    /// </para>
    /// <para>
    /// Vertices carry positions, normals and UV0 but <b>no tangents</b>. Normals are not needed by
    /// URP Unlit's forward pass, but they are read by its DepthNormals pass, which this project runs
    /// because SSAO is enabled with <c>Source: DepthNormals</c>. Omitting them would feed garbage
    /// into the ambient-occlusion normals buffer.
    /// </para>
    /// </remarks>
    [NoAutoStaticsCleanup]
    public static class PetalMeshBuilder
    {
        public const string PetalMeshName = "PG_PetalMesh";
        public const string CenterMeshName = "PG_CenterMesh";

        public const int MinCenterSegments = 3;
        public const int MaxCenterSegments = 64;

        /// <summary>Every vertex shares this normal; the mesh is flat and single-sided.</summary>
        private static readonly Vector3 FaceNormal = new Vector3(0f, 0f, -1f);

        // Scratch buffers reused across calls so repeated rebuilds allocate no garbage.
        // Opted out of automatic statics cleanup: they hold no state between calls — every entry
        // point clears them first — so resetting them on code reload would only re-allocate.
        [NoAutoStaticsCleanup] private static readonly List<Vector3> Positions = new List<Vector3>(160);
        [NoAutoStaticsCleanup] private static readonly List<Vector3> Normals = new List<Vector3>(160);
        [NoAutoStaticsCleanup] private static readonly List<Vector2> Uvs = new List<Vector2>(160);
        [NoAutoStaticsCleanup] private static readonly List<int> Indices = new List<int>(320);

        public static Mesh CreatePetal(PetalShapeParameters shapeParameters)
        {
            Mesh mesh = new Mesh { name = PetalMeshName };
            WritePetal(mesh, shapeParameters);
            return mesh;
        }

        public static Mesh CreateCenterDisc(float radius, int segments)
        {
            Mesh mesh = new Mesh { name = CenterMeshName };
            WriteCenterDisc(mesh, radius, segments);
            return mesh;
        }

        /// <summary>
        /// Rewrites <paramref name="target"/> in place. This is the primary path — rebuilding a
        /// flower reuses its existing <see cref="Mesh"/> objects rather than allocating new ones.
        /// </summary>
        public static void WritePetal(Mesh target, PetalShapeParameters shapeParameters)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            PetalShapeParameters shape = shapeParameters.Clamped();
            int segments = shape.LengthSegments;

            ClearBuffers();

            // Paired left/right vertices per row, then a single collapsed tip vertex.
            // Row 0 sits at t = 0 where the half-width is zero, so it collapses to a point and
            // yields one degenerate triangle. That is deliberate: it costs nothing on the GPU and
            // keeps this loop branchless for any base roundness.
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                float halfWidth = shape.HalfWidthAt(t);
                float y = t * shape.Length;

                Positions.Add(new Vector3(-halfWidth, y, 0f));
                Positions.Add(new Vector3(halfWidth, y, 0f));
                Normals.Add(FaceNormal);
                Normals.Add(FaceNormal);
                Uvs.Add(new Vector2(0f, t));
                Uvs.Add(new Vector2(1f, t));
            }

            int tipIndex = 2 * segments;
            Positions.Add(new Vector3(0f, shape.Length, 0f));
            Normals.Add(FaceNormal);
            Uvs.Add(new Vector2(0.5f, 1f));

            // Wound clockwise as drawn in XY (+X right, +Y up) so the right-hand-rule normal is -Z.
            for (int i = 0; i < segments - 1; i++)
            {
                int lowerLeft = 2 * i;
                int lowerRight = lowerLeft + 1;
                int upperLeft = 2 * (i + 1);
                int upperRight = upperLeft + 1;

                Indices.Add(lowerLeft);
                Indices.Add(upperLeft);
                Indices.Add(lowerRight);

                Indices.Add(lowerRight);
                Indices.Add(upperLeft);
                Indices.Add(upperRight);
            }

            int lastLeft = 2 * (segments - 1);
            Indices.Add(lastLeft);
            Indices.Add(tipIndex);
            Indices.Add(lastLeft + 1);

            Apply(target, PetalMeshName);
        }

        /// <summary>Rewrites <paramref name="target"/> as a flat triangle-fan disc facing -Z.</summary>
        public static void WriteCenterDisc(Mesh target, float radius, int segments)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            radius = Mathf.Max(radius, PetalShapeParameters.MinSize);
            segments = Mathf.Clamp(segments, MinCenterSegments, MaxCenterSegments);

            ClearBuffers();

            Positions.Add(Vector3.zero);
            Normals.Add(FaceNormal);
            Uvs.Add(new Vector2(0.5f, 0.5f));

            for (int k = 0; k < segments; k++)
            {
                float angle = (float)k / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                Positions.Add(new Vector3(cos * radius, sin * radius, 0f));
                Normals.Add(FaceNormal);
                Uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            for (int k = 0; k < segments; k++)
            {
                int current = 1 + k;
                int next = 1 + (k + 1) % segments;

                Indices.Add(0);
                Indices.Add(next);
                Indices.Add(current);
            }

            Apply(target, CenterMeshName);
        }

        private static void ClearBuffers()
        {
            Positions.Clear();
            Normals.Clear();
            Uvs.Clear();
            Indices.Clear();
        }

        private static void Apply(Mesh target, string meshName)
        {
            target.Clear();
            target.name = meshName;
            target.SetVertices(Positions);
            target.SetNormals(Normals);
            target.SetUVs(0, Uvs);
            target.SetTriangles(Indices, 0, true);

            // Never RecalculateNormals() — a flat strip with a degenerate row yields zero-length
            // normals. Never Optimize() — it reorders vertices the tests assert positions against.
            // Keep the mesh CPU-readable: Edit-mode rebuilds and EditMode tests inspect it.
            target.UploadMeshData(false);
        }
    }
}
