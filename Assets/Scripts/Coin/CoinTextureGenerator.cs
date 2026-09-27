using System;
using UnityEngine;

namespace Toss.Coin
{
    public static class CoinTextureGenerator
    {
        private static readonly Vector2[] HeadsPolygon = new Vector2[]
        {
            new Vector2(-0.06f,  0.48f), // Crown top
            new Vector2(-0.18f,  0.40f), // Forehead top
            new Vector2(-0.28f,  0.28f), // Brow ridge
            new Vector2(-0.26f,  0.21f), // Nose bridge dip (nasion)
            new Vector2(-0.40f,  0.08f), // Nose tip (pointing LEFT)
            new Vector2(-0.31f,  0.03f), // Nose base
            new Vector2(-0.34f, -0.04f), // Upper lip
            new Vector2(-0.27f, -0.07f), // Mouth slit
            new Vector2(-0.32f, -0.12f), // Lower lip
            new Vector2(-0.26f, -0.16f), // Mentolabial groove
            new Vector2(-0.33f, -0.24f), // Chin tip
            new Vector2(-0.29f, -0.31f), // Under chin
            new Vector2(-0.16f, -0.36f), // Throat
            new Vector2(-0.14f, -0.52f), // Front neck base
            new Vector2( 0.16f, -0.52f), // Truncated neck base bottom
            new Vector2( 0.26f, -0.36f), // Back neck / nape
            new Vector2( 0.35f, -0.18f), // Lower hair bun / occiput
            new Vector2( 0.38f,  0.04f), // Back of head
            new Vector2( 0.32f,  0.26f), // Upper rear crown
            new Vector2( 0.18f,  0.42f), // Top crown rear
            new Vector2( 0.06f,  0.48f)  // Crown apex
        };

        private static readonly Vector2[] EagleHalf = new Vector2[]
        {
            new Vector2( 0.00f,  0.44f), // Crest tip
            new Vector2(-0.06f,  0.39f), // Crown curve
            new Vector2(-0.12f,  0.33f), // Beak top
            new Vector2(-0.19f,  0.30f), // Beak tip (sharp eagle beak pointing left)
            new Vector2(-0.11f,  0.25f), // Under beak
            new Vector2(-0.08f,  0.20f), // Throat to breast
            new Vector2(-0.20f,  0.26f), // Wing shoulder arch
            new Vector2(-0.42f,  0.40f), // Primary feather tip 1
            new Vector2(-0.38f,  0.28f), // Notch 1
            new Vector2(-0.48f,  0.25f), // Primary feather tip 2
            new Vector2(-0.40f,  0.15f), // Notch 2
            new Vector2(-0.44f,  0.08f), // Primary feather tip 3
            new Vector2(-0.34f, -0.01f), // Lower wing curve
            new Vector2(-0.22f, -0.08f), // Wing inner joint
            new Vector2(-0.16f, -0.18f), // Talon / leg anchor
            new Vector2(-0.22f, -0.30f), // Outer tail feather
            new Vector2(-0.12f, -0.26f), // Tail notch
            new Vector2(-0.10f, -0.42f), // Center tail feather edge
            new Vector2( 0.00f, -0.45f)  // Central tail apex
        };

        public static void GenerateTextures(CoinVisualConfig config, out Texture2D normalMap, out Texture2D aoMap, int width = 1024, int height = 512)
        {
            if (config == null) config = new CoinVisualConfig();

            float[,] heightField = new float[width, height];
            int halfW = width / 2;

            // Generate Heightfield for Left (Heads) and Right (Tails)
            for (int y = 0; y < height; y++)
            {
                float ny = (y - height * 0.5f) / (height * 0.5f); // [-1, 1]

                for (int x = 0; x < width; x++)
                {
                    float nx;
                    bool isTails = x >= halfW;

                    if (!isTails)
                    {
                        // Heads tile: center at (width * 0.25, height * 0.5)
                        nx = (x - halfW * 0.5f) / (halfW * 0.5f);
                    }
                    else
                    {
                        // Tails tile: center at (width * 0.75, height * 0.5)
                        nx = (x - halfW * 1.5f) / (halfW * 0.5f);
                    }

                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float h = 0.05f; // Base basin height

                    if (r <= 0.98f)
                    {
                        // 1. Concentric / Radial Brushed Mint Luster Grain
                        float theta = Mathf.Atan2(ny, nx);
                        float lathe = Mathf.Sin(r * 320.0f) * 0.015f;
                        float radial = Mathf.Cos(theta * 96.0f) * 0.012f;
                        float brushed = (lathe + radial) * (config.brushedGrainStrength / 0.025f);

                        // 2. Beaded / Stepped Inner Rim at r ≈ 0.88 - 0.94
                        float rimBead = 0.0f;
                        if (r >= 0.84f && r <= 0.94f)
                        {
                            float rimDist = Mathf.Abs(r - 0.89f);
                            rimBead = Mathf.Clamp01(1.0f - (rimDist / 0.05f)) * 0.15f;
                        }

                        // 3. Stylized Bas-Relief
                        float relief = 0.0f;
                        if (!isTails)
                        {
                            // Heads: Left-Facing Classical Profile
                            Vector2 pt = new Vector2(nx, ny);
                            if (PointInPolygon(pt, HeadsPolygon))
                            {
                                float distToEdge = DistanceToPolygon(pt, HeadsPolygon);
                                float bevel = Mathf.Clamp01(distToEdge / 0.06f);
                                relief = Mathf.Sin(bevel * (Mathf.PI * 0.5f)) * 0.50f;

                                // Subtle hair wave ridge accent
                                if (nx > -0.05f && ny > 0.0f)
                                {
                                    float hairWave = Mathf.Sin((nx * 4.0f + ny * 6.0f) * Mathf.PI) * 0.06f;
                                    relief += Mathf.Max(0f, hairWave);
                                }
                            }
                        }
                        else
                        {
                            // Tails: Heraldic Eagle with Spread Wings (Symmetrical)
                            Vector2 pt = new Vector2(Mathf.Abs(nx), ny); // Mirror X for symmetry
                            if (PointInPolygon(pt, EagleHalf))
                            {
                                float distToEdge = DistanceToPolygon(pt, EagleHalf);
                                float bevel = Mathf.Clamp01(distToEdge / 0.05f);
                                relief = Mathf.Sin(bevel * (Mathf.PI * 0.5f)) * 0.48f;

                                // Central heraldic chest shield emblem
                                if (Mathf.Abs(nx) <= 0.11f && ny >= -0.15f && ny <= 0.18f)
                                {
                                    float shieldBevel = Mathf.Clamp01((0.11f - Mathf.Abs(nx)) / 0.03f);
                                    relief += shieldBevel * 0.12f;
                                }
                            }
                        }

                        h = Mathf.Clamp01(h + rimBead + relief * (config.reliefStrength / 1.6f) + brushed);
                    }

                    heightField[x, y] = h;
                }
            }

            // Normal Map and Ambient Occlusion Output Textures
            normalMap = new Texture2D(width, height, TextureFormat.RGBA32, true, true)
            {
                name = "Tex_TossCoin_Normal",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            aoMap = new Texture2D(width, height, TextureFormat.RGBA32, true, true)
            {
                name = "Tex_TossCoin_AO",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] normalPixels = new Color32[width * height];
            Color32[] aoPixels = new Color32[width * height];

            float strength = 18.0f * (config.reliefStrength / 1.6f);

            for (int y = 0; y < height; y++)
            {
                int yPrev = Mathf.Max(0, y - 1);
                int yNext = Mathf.Min(height - 1, y + 1);

                for (int x = 0; x < width; x++)
                {
                    int xPrev = Mathf.Max(0, x - 1);
                    int xNext = Mathf.Min(width - 1, x + 1);

                    // Central difference gradient
                    float dx = (heightField[xNext, y] - heightField[xPrev, y]) * strength;
                    float dy = (heightField[x, yNext] - heightField[x, yPrev]) * strength;

                    Vector3 n = new Vector3(-dx, -dy, 1.0f).normalized;

                    // Standard normal map encoding: [0, 1] mapped from [-1, 1]
                    byte rByte = (byte)Mathf.Clamp((n.x * 0.5f + 0.5f) * 255.0f, 0f, 255f);
                    byte gByte = (byte)Mathf.Clamp((n.y * 0.5f + 0.5f) * 255.0f, 0f, 255f);
                    byte bByte = (byte)Mathf.Clamp((n.z * 0.5f + 0.5f) * 255.0f, 0f, 255f);

                    int idx = y * width + x;
                    normalPixels[idx] = new Color32(rByte, gByte, bByte, 255);

                    // Ambient Occlusion: crevice shadowing based on gradient magnitude and basin depth
                    float slope = Mathf.Sqrt(dx * dx + dy * dy);
                    float hVal = heightField[x, y];
                    float ao = Mathf.Clamp01(1.0f - (slope * 0.12f + (1.0f - hVal) * 0.15f));
                    byte aoByte = (byte)(ao * 255.0f);
                    aoPixels[idx] = new Color32(aoByte, aoByte, aoByte, 255);
                }
            }

            normalMap.SetPixels32(normalPixels);
            normalMap.Apply(true, false);

            aoMap.SetPixels32(aoPixels);
            aoMap.Apply(true, false);
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            int j = poly.Length - 1;
            for (int i = 0; i < poly.Length; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static float DistanceToPolygon(Vector2 p, Vector2[] poly)
        {
            float minSqrDist = float.MaxValue;
            int j = poly.Length - 1;
            for (int i = 0; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j];
                Vector2 b = poly[i];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.00001f, ab.sqrMagnitude));
                Vector2 proj = a + t * ab;
                float sqrDist = (p - proj).sqrMagnitude;
                if (sqrDist < minSqrDist) minSqrDist = sqrDist;
            }
            return Mathf.Sqrt(minSqrDist);
        }
    }
}
