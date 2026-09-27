using System;
using System.Collections.Generic;
using UnityEngine;

namespace Toss.Coin
{
    public static class CoinMeshGenerator
    {
        public static Mesh GenerateCoinMesh(CoinVisualConfig config)
        {
            if (config == null) config = new CoinVisualConfig();

            var mesh = new Mesh
            {
                name = "Mesh_TossCoin_Procedural"
            };

            float outerRadius = Mathf.Max(0.005f, config.diameter * 0.5f);
            float halfThickness = Mathf.Max(0.0005f, config.thickness * 0.5f);
            int ridgeCount = Mathf.Clamp(config.edgeRidgeCount, 12, 120);
            float ridgeDepth = Mathf.Clamp(config.edgeRidgeDepth, 0.00005f, 0.001f);
            float rimWidth = Mathf.Clamp(config.rimWidth, 0.0005f, outerRadius * 0.3f);
            float rimHeight = Mathf.Clamp(config.rimHeight, 0.00005f, halfThickness * 0.8f);

            float bevelSize = Mathf.Min(0.0003f, halfThickness * 0.25f);
            float faceRadius = outerRadius - rimWidth;
            float rimOuterRadius = outerRadius - bevelSize;

            int stepsPerRidge = 4;
            int segments = ridgeCount * stepsPerRidge; // e.g. 48 * 4 = 192

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Calculate radial profiles for each segment
            float[] cosTable = new float[segments + 1];
            float[] sinTable = new float[segments + 1];
            float[] ridgeRadii = new float[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i % segments) * (Mathf.PI * 2.0f / segments);
                cosTable[i] = Mathf.Cos(angle);
                sinTable[i] = Mathf.Sin(angle);

                // Reeded tooth profile: trapezoidal wave
                float ridgeWave = Mathf.Cos(ridgeCount * angle);
                float ridgeVal = Mathf.Clamp(ridgeWave * 2.2f, -1.0f, 1.0f);
                ridgeRadii[i] = outerRadius + ridgeVal * (ridgeDepth * 0.5f);
            }

            // ==========================================
            // 1. FRONT FACE (Heads) - Basin & Raised Rim
            // ==========================================
            int frontCenterIndex = vertices.Count;
            // Center vertex
            float frontBasinZ = halfThickness - rimHeight;
            vertices.Add(new Vector3(0f, 0f, frontBasinZ));
            normals.Add(Vector3.forward);
            uvs.Add(new Vector2(0.25f, 0.50f));

            // Ring 0: Face basin inner boundary (at faceRadius)
            int frontBasinRingStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float x = faceRadius * cosTable[i];
                float y = faceRadius * sinTable[i];
                vertices.Add(new Vector3(x, y, frontBasinZ));
                normals.Add(Vector3.forward);

                float u = 0.25f + (x / (outerRadius * 2.0f)) * 0.46f;
                float v = 0.50f + (y / (outerRadius * 2.0f)) * 0.92f;
                uvs.Add(new Vector2(u, v));
            }

            // Triangles for front basin fan
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(frontCenterIndex);
                triangles.Add(frontBasinRingStart + i);
                triangles.Add(frontBasinRingStart + i + 1);
            }

            // Ring 1: Inner rim top (at faceRadius, z = halfThickness) - slope up
            int frontRimInnerStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float x = faceRadius * cosTable[i];
                float y = faceRadius * sinTable[i];
                vertices.Add(new Vector3(x, y, halfThickness));
                Vector3 n = (Vector3.forward + new Vector3(-cosTable[i], -sinTable[i], 0f) * 0.5f).normalized;
                normals.Add(n);
                uvs.Add(new Vector2(0.25f + (x / (outerRadius * 2.0f)) * 0.46f, 0.50f + (y / (outerRadius * 2.0f)) * 0.92f));
            }

            // Triangles for inner rim wall
            for (int i = 0; i < segments; i++)
            {
                int b0 = frontBasinRingStart + i;
                int b1 = frontBasinRingStart + i + 1;
                int t0 = frontRimInnerStart + i;
                int t1 = frontRimInnerStart + i + 1;

                triangles.Add(b0);
                triangles.Add(t0);
                triangles.Add(t1);

                triangles.Add(b0);
                triangles.Add(t1);
                triangles.Add(b1);
            }

            // Ring 2: Rim flat top outer boundary (at rimOuterRadius, z = halfThickness)
            int frontRimOuterStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float x = rimOuterRadius * cosTable[i];
                float y = rimOuterRadius * sinTable[i];
                vertices.Add(new Vector3(x, y, halfThickness));
                normals.Add(Vector3.forward);
                uvs.Add(new Vector2(0.25f + (x / (outerRadius * 2.0f)) * 0.48f, 0.50f + (y / (outerRadius * 2.0f)) * 0.96f));
            }

            // Triangles for flat rim top ring
            for (int i = 0; i < segments; i++)
            {
                int i0 = frontRimInnerStart + i;
                int i1 = frontRimInnerStart + i + 1;
                int o0 = frontRimOuterStart + i;
                int o1 = frontRimOuterStart + i + 1;

                triangles.Add(i0);
                triangles.Add(o0);
                triangles.Add(o1);

                triangles.Add(i0);
                triangles.Add(o1);
                triangles.Add(i1);
            }

            // ==========================================
            // 2. REEDED EDGE (48 Ridges) & Chamfers
            // ==========================================
            // Ring 3: Top chamfer start on reeded edge (at ridgeRadii[i], z = halfThickness - bevelSize)
            int edgeTopStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float r = ridgeRadii[i];
                float x = r * cosTable[i];
                float y = r * sinTable[i];
                vertices.Add(new Vector3(x, y, halfThickness - bevelSize));
                Vector3 n = (Vector3.forward + new Vector3(cosTable[i], sinTable[i], 0f)).normalized;
                normals.Add(n);
                uvs.Add(new Vector2((float)i / segments, 0.95f));
            }

            // Triangles for front outer bevel
            for (int i = 0; i < segments; i++)
            {
                int r0 = frontRimOuterStart + i;
                int r1 = frontRimOuterStart + i + 1;
                int e0 = edgeTopStart + i;
                int e1 = edgeTopStart + i + 1;

                triangles.Add(r0);
                triangles.Add(e0);
                triangles.Add(e1);

                triangles.Add(r0);
                triangles.Add(e1);
                triangles.Add(r1);
            }

            // Ring 4: Bottom chamfer end on reeded edge (at ridgeRadii[i], z = -halfThickness + bevelSize)
            int edgeBottomStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float r = ridgeRadii[i];
                float x = r * cosTable[i];
                float y = r * sinTable[i];
                vertices.Add(new Vector3(x, y, -halfThickness + bevelSize));
                Vector3 n = new Vector3(cosTable[i], sinTable[i], 0f);
                normals.Add(n);
                uvs.Add(new Vector2((float)i / segments, 0.05f));
            }

            // Triangles for reeded edge cylinder
            for (int i = 0; i < segments; i++)
            {
                int t0 = edgeTopStart + i;
                int t1 = edgeTopStart + i + 1;
                int b0 = edgeBottomStart + i;
                int b1 = edgeBottomStart + i + 1;

                triangles.Add(t0);
                triangles.Add(b0);
                triangles.Add(b1);

                triangles.Add(t0);
                triangles.Add(b1);
                triangles.Add(t1);
            }

            // ==========================================
            // 3. BACK FACE (Tails) - Raised Rim & Basin
            // ==========================================
            // Ring 5: Back rim outer boundary (at rimOuterRadius, z = -halfThickness)
            int backRimOuterStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float x = rimOuterRadius * cosTable[i];
                float y = rimOuterRadius * sinTable[i];
                vertices.Add(new Vector3(x, y, -halfThickness));
                Vector3 n = (-Vector3.forward + new Vector3(cosTable[i], sinTable[i], 0f)).normalized;
                normals.Add(n);
                uvs.Add(new Vector2(0.75f - (x / (outerRadius * 2.0f)) * 0.48f, 0.50f + (y / (outerRadius * 2.0f)) * 0.96f));
            }

            // Triangles for back outer bevel
            for (int i = 0; i < segments; i++)
            {
                int e0 = edgeBottomStart + i;
                int e1 = edgeBottomStart + i + 1;
                int r0 = backRimOuterStart + i;
                int r1 = backRimOuterStart + i + 1;

                triangles.Add(e0);
                triangles.Add(r0);
                triangles.Add(r1);

                triangles.Add(e0);
                triangles.Add(r1);
                triangles.Add(e1);
            }

            // Ring 6: Back rim inner boundary (at faceRadius, z = -halfThickness)
            int backRimInnerStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float x = faceRadius * cosTable[i];
                float y = faceRadius * sinTable[i];
                vertices.Add(new Vector3(x, y, -halfThickness));
                normals.Add(-Vector3.forward);
                uvs.Add(new Vector2(0.75f - (x / (outerRadius * 2.0f)) * 0.46f, 0.50f + (y / (outerRadius * 2.0f)) * 0.92f));
            }

            // Triangles for flat back rim
            for (int i = 0; i < segments; i++)
            {
                int o0 = backRimOuterStart + i;
                int o1 = backRimOuterStart + i + 1;
                int i0 = backRimInnerStart + i;
                int i1 = backRimInnerStart + i + 1;

                triangles.Add(o0);
                triangles.Add(i0);
                triangles.Add(i1);

                triangles.Add(o0);
                triangles.Add(i1);
                triangles.Add(o1);
            }

            // Ring 7: Back face basin inner boundary (at faceRadius, z = -halfThickness + rimHeight)
            float backBasinZ = -halfThickness + rimHeight;
            int backBasinRingStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float x = faceRadius * cosTable[i];
                float y = faceRadius * sinTable[i];
                vertices.Add(new Vector3(x, y, backBasinZ));
                normals.Add(-Vector3.forward);
                uvs.Add(new Vector2(0.75f - (x / (outerRadius * 2.0f)) * 0.46f, 0.50f + (y / (outerRadius * 2.0f)) * 0.92f));
            }

            // Triangles for back rim inner wall
            for (int i = 0; i < segments; i++)
            {
                int t0 = backRimInnerStart + i;
                int t1 = backRimInnerStart + i + 1;
                int b0 = backBasinRingStart + i;
                int b1 = backBasinRingStart + i + 1;

                triangles.Add(t0);
                triangles.Add(b0);
                triangles.Add(b1);

                triangles.Add(t0);
                triangles.Add(b1);
                triangles.Add(t1);
            }

            // Center vertex for back basin fan
            int backCenterIndex = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, backBasinZ));
            normals.Add(-Vector3.forward);
            uvs.Add(new Vector2(0.75f, 0.50f));

            // Triangles for back basin fan
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(backCenterIndex);
                triangles.Add(backBasinRingStart + i + 1);
                triangles.Add(backBasinRingStart + i);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
