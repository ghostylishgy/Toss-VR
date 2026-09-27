using System;
using UnityEngine;

namespace Toss.Coin
{
    /// <summary>
    /// Procedural texture generator for Toss P1 Coin bas-relief identity.
    /// Implements:
    /// - Heads: Classical sculptural cameo bust (noble Grecian profile, large curved volumes, C1-smooth relief).
    /// - Tails: Heroic architectural upward-sweeping wing blades with razor-sharp 3D faceted spine creases.
    /// - Basin & Rim: Silky satin silver finish with narrow precision stepped rim.
    /// </summary>
    public static class CoinTextureGenerator
    {
        // -------------------------------------------------------------------
        // 1. HEADS: Classical Noble Cameo Bust Silhouette
        // -------------------------------------------------------------------
        private static readonly Vector2[] HeadsProfile = new Vector2[]
        {
            new Vector2( 0.06f,  0.54f), // Top crown apex
            new Vector2(-0.08f,  0.54f),
            new Vector2(-0.18f,  0.48f),
            new Vector2(-0.24f,  0.40f), // Hairline / forehead top
            new Vector2(-0.28f,  0.32f), // Forehead slope
            new Vector2(-0.30f,  0.22f),
            new Vector2(-0.32f,  0.14f), // Brow
            new Vector2(-0.33f,  0.10f), // Nasion dip
            new Vector2(-0.30f,  0.06f), // Upper nose bridge
            new Vector2(-0.35f, -0.01f),
            new Vector2(-0.41f, -0.07f),
            new Vector2(-0.45f, -0.10f), // Straight Grecian nose tip
            new Vector2(-0.42f, -0.13f), // Columella / nostril
            new Vector2(-0.37f, -0.12f),
            new Vector2(-0.34f, -0.13f),
            new Vector2(-0.36f, -0.15f), // Philtrum
            new Vector2(-0.40f, -0.18f), // Upper lip
            new Vector2(-0.33f, -0.19f), // Mouth fissure indent
            new Vector2(-0.36f, -0.22f), // Lower lip
            new Vector2(-0.38f, -0.26f), // Mentolabial groove
            new Vector2(-0.34f, -0.28f),
            new Vector2(-0.30f, -0.31f), // Firm sculpted chin apex
            new Vector2(-0.35f, -0.35f),
            new Vector2(-0.34f, -0.42f),
            new Vector2(-0.27f, -0.44f), // Submental jawline
            new Vector2(-0.20f, -0.46f), // Throat
            new Vector2(-0.14f, -0.54f), // Front neck
            new Vector2(-0.13f, -0.60f),
            new Vector2(-0.16f, -0.68f),
            new Vector2(-0.22f, -0.74f), // Bust truncation front corner
            new Vector2(-0.05f, -0.78f), // Truncation bottom arc
            new Vector2( 0.14f, -0.74f),
            new Vector2( 0.25f, -0.63f), // Rear truncation corner
            new Vector2( 0.21f, -0.48f), // Back neck
            new Vector2( 0.22f, -0.33f),
            new Vector2( 0.25f, -0.20f), // Nape
            new Vector2( 0.40f, -0.14f), // Chignon bun lower contour
            new Vector2( 0.48f, -0.02f), // Bun rear apex
            new Vector2( 0.48f,  0.10f), // Bun upper contour
            new Vector2( 0.48f,  0.22f),
            new Vector2( 0.38f,  0.30f), // Bun crown junction
            new Vector2( 0.28f,  0.32f), // Rear crown
            new Vector2( 0.22f,  0.42f),
            new Vector2( 0.15f,  0.50f)
        };

        // -------------------------------------------------------------------
        // 2. TAILS: Central Diamond Spine & Heroic Upward-Sweeping Wings
        // -------------------------------------------------------------------
        private static readonly Vector2[] DiamondCore = new Vector2[]
        {
            new Vector2( 0.000f,  0.42f), // Top needle
            new Vector2( 0.088f,  0.05f), // Right waist
            new Vector2( 0.000f, -0.42f), // Bottom needle
            new Vector2(-0.088f,  0.05f)  // Left waist
        };

        // Wing Blade 1 (Top Primary Blade, sweeps up to 0.60, 0.62)
        private static readonly Vector2[] WingBlade1 = new Vector2[]
        {
            new Vector2(0.08f, 0.15f),
            new Vector2(0.14f, 0.25f),
            new Vector2(0.20f, 0.36f),
            new Vector2(0.28f, 0.45f),
            new Vector2(0.38f, 0.54f),
            new Vector2(0.48f, 0.60f),
            new Vector2(0.60f, 0.62f), // Tip
            new Vector2(0.51f, 0.51f),
            new Vector2(0.42f, 0.40f),
            new Vector2(0.33f, 0.30f),
            new Vector2(0.25f, 0.20f),
            new Vector2(0.16f, 0.14f),
            new Vector2(0.08f, 0.08f)
        };

        // Wing Blade 2 (Middle Primary Blade, sweeps up to 0.66, 0.40)
        private static readonly Vector2[] WingBlade2 = new Vector2[]
        {
            new Vector2(0.08f, 0.06f),
            new Vector2(0.18f, 0.15f),
            new Vector2(0.28f, 0.24f),
            new Vector2(0.38f, 0.30f),
            new Vector2(0.48f, 0.35f),
            new Vector2(0.58f, 0.38f),
            new Vector2(0.66f, 0.40f), // Tip
            new Vector2(0.57f, 0.31f),
            new Vector2(0.48f, 0.22f),
            new Vector2(0.38f, 0.16f),
            new Vector2(0.28f, 0.10f),
            new Vector2(0.17f, 0.04f),
            new Vector2(0.07f,-0.02f)
        };

        // Wing Blade 3 (Lower Primary Blade, sweeps up to 0.58, 0.16)
        private static readonly Vector2[] WingBlade3 = new Vector2[]
        {
            new Vector2(0.07f,-0.04f),
            new Vector2(0.16f, 0.01f),
            new Vector2(0.26f, 0.06f),
            new Vector2(0.35f, 0.09f),
            new Vector2(0.44f, 0.12f),
            new Vector2(0.52f, 0.14f),
            new Vector2(0.58f, 0.16f), // Tip
            new Vector2(0.50f, 0.09f),
            new Vector2(0.42f, 0.03f),
            new Vector2(0.33f,-0.01f),
            new Vector2(0.25f,-0.05f),
            new Vector2(0.15f,-0.08f),
            new Vector2(0.06f,-0.12f)
        };

        // Wing Blade 4 (Base Spur, curves to 0.40, -0.06)
        private static readonly Vector2[] WingBlade4 = new Vector2[]
        {
            new Vector2(0.05f,-0.14f),
            new Vector2(0.11f,-0.11f),
            new Vector2(0.18f,-0.08f),
            new Vector2(0.24f,-0.07f),
            new Vector2(0.30f,-0.06f),
            new Vector2(0.40f,-0.06f), // Tip
            new Vector2(0.35f,-0.11f),
            new Vector2(0.30f,-0.16f),
            new Vector2(0.24f,-0.19f),
            new Vector2(0.18f,-0.22f),
            new Vector2(0.10f,-0.22f),
            new Vector2(0.03f,-0.22f)
        };

        public static void GenerateTextures(CoinVisualConfig config, out Texture2D normalMap, out Texture2D aoMap, int width = 1024, int height = 512)
        {
            if (config == null) config = new CoinVisualConfig();

            int halfW = width / 2;

            // 1. Rasterize binary masks
            float[,] maskHeads = new float[halfW, height];
            float[,] maskTails = new float[halfW, height];

            for (int y = 0; y < height; y++)
            {
                float ny = (y - height * 0.5f) / (height * 0.5f); // [-1, 1]

                for (int x = 0; x < halfW; x++)
                {
                    float nx = (x - halfW * 0.5f) / (halfW * 0.5f); // [-1, 1]
                    Vector2 p = new Vector2(nx, ny);

                    // Heads Cameo Silhouette
                    if (PointInPolygon(p, HeadsProfile))
                    {
                        maskHeads[x, y] = 1.0f;
                    }

                    // Tails Symmetrical Wing Emblem
                    Vector2 pSym = new Vector2(Mathf.Abs(nx), ny);
                    if (PointInPolygon(p, DiamondCore) ||
                        PointInPolygon(pSym, WingBlade1) ||
                        PointInPolygon(pSym, WingBlade2) ||
                        PointInPolygon(pSym, WingBlade3) ||
                        PointInPolygon(pSym, WingBlade4))
                    {
                        maskTails[x, y] = 1.0f;
                    }
                }
            }

            // 2. Smooth separable box blur for bas-relief bevel bases
            float[,] blurHeads = BlurSeparable(maskHeads, halfW, height, 5);
            blurHeads = BlurSeparable(blurHeads, halfW, height, 4);

            float[,] blurTails = BlurSeparable(maskTails, halfW, height, 3);
            blurTails = BlurSeparable(blurTails, halfW, height, 2);

            // 3. Assemble Heightfield
            float[,] heightField = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                float ny = (y - height * 0.5f) / (height * 0.5f);

                for (int x = 0; x < width; x++)
                {
                    bool isTails = x >= halfW;
                    int lx = isTails ? (x - halfW) : x;
                    float nx = (lx - halfW * 0.5f) / (halfW * 0.5f);
                    float r = Mathf.Sqrt(nx * nx + ny * ny);

                    if (r > 0.985f)
                    {
                        heightField[x, y] = 0.0f;
                        continue;
                    }

                    // Satin basin floor
                    float h = 0.16f;

                    // Refined precision rim: r in [0.89, 0.94] with stepped bevel at [0.86, 0.89]
                    if (r >= 0.89f && r <= 0.94f)
                    {
                        float rimDist = Mathf.Abs(r - 0.915f);
                        float rimT = Mathf.Clamp01(1.0f - (rimDist / 0.025f));
                        h += rimT * 0.22f;
                    }
                    else if (r >= 0.86f && r < 0.89f)
                    {
                        float stepT = (r - 0.86f) / 0.03f;
                        h += stepT * 0.06f;
                    }

                    // Silky satin brush texture
                    float theta = Mathf.Atan2(ny, nx);
                    float brushScale = config.brushedGrainStrength / 0.025f;
                    float satinBrush = (Mathf.Sin(r * 16.0f) * 0.0006f + Mathf.Cos(theta * 10.0f) * 0.0006f) * brushScale;
                    h += satinBrush;

                    if (!isTails)
                    {
                        // ====================================================
                        // HEADS: Classical Cameo Bust (C1 Continuous Volumes)
                        // ====================================================
                        float b = blurHeads[lx, y];
                        if (b > 0.001f)
                        {
                            float relief = Mathf.Pow(b, 0.50f) * 0.50f;

                            // 1. Cranium Volume: C1 dome
                            float cranD = Mathf.Sqrt(Mathf.Pow((nx - 0.02f) / 0.38f, 2) + Mathf.Pow((ny - 0.14f) / 0.38f, 2));
                            relief += C1Dome(cranD, 1.0f) * 0.22f * b;

                            // 2. Forehead to Brow slope: C1 dome
                            float fhD = Mathf.Sqrt(Mathf.Pow((nx + 0.18f) / 0.16f, 2) + Mathf.Pow((ny - 0.18f) / 0.18f, 2));
                            relief += C1Dome(fhD, 1.0f) * 0.08f * b;

                            // 3. Nose Bridge Ridge: C1 smooth crest
                            float vxN = -0.15f, vyN = -0.16f;
                            float lN = Mathf.Sqrt(vxN * vxN + vyN * vyN);
                            float unx = vxN / lN, uny = vyN / lN;
                            float dxn = nx - (-0.30f), dyn = ny - 0.06f;
                            float projN = dxn * unx + dyn * uny;
                            if (projN >= 0f && projN <= lN)
                            {
                                float perpN = Mathf.Abs(-dxn * uny + dyn * unx);
                                if (perpN < 0.045f)
                                {
                                    float ridgeProf = C1Dome(perpN, 0.045f);
                                    float tEnd = Mathf.Pow(Mathf.Sin(Mathf.PI * projN / lN), 2);
                                    relief += ridgeProf * 0.10f * tEnd * b;
                                }
                            }

                            // 4. Cheekbone prominence
                            float ckD = Mathf.Sqrt(Mathf.Pow((nx + 0.12f) / 0.18f, 2) + Mathf.Pow((ny + 0.06f) / 0.16f, 2));
                            relief += C1Dome(ckD, 1.0f) * 0.08f * b;

                            // 5. Chin fullness
                            float cnD = Mathf.Sqrt(Mathf.Pow((nx + 0.25f) / 0.12f, 2) + Mathf.Pow((ny + 0.33f) / 0.12f, 2));
                            relief += C1Dome(cnD, 1.0f) * 0.08f * b;

                            // 6. Neck cylinder
                            float neckFade = Mathf.SmoothStep(-0.06f, -0.25f, ny);
                            if (neckFade > 0.001f)
                            {
                                float neckCenterX = -0.14f - (ny + 0.20f) * 0.12f;
                                float distToNeck = Mathf.Abs(nx - neckCenterX);
                                float neckCyl = C1Dome(distToNeck, 0.22f) * 0.12f;
                                float diagNeck = Mathf.Abs(0.48f * nx + 0.88f * (ny + 0.38f));
                                float neckMuscle = C1Dome(diagNeck, 0.15f) * 0.06f;
                                relief += (neckCyl + neckMuscle) * neckFade * b;
                            }

                            // 7. Voluptuous Chignon Bun
                            float bunD = Mathf.Sqrt(Mathf.Pow((nx - 0.34f) / 0.18f, 2) + Mathf.Pow((ny + 0.02f) / 0.18f, 2));
                            if (bunD < 1.0f)
                            {
                                float bunVol = C1Dome(bunD, 1.0f) * 0.30f;
                                float bunLoop = Mathf.Cos(bunD * Mathf.PI * 3.0f) * 0.035f * Mathf.Pow(1.0f - bunD, 2);
                                relief += (bunVol + bunLoop) * b;
                            }

                            // 8. Flowing Hair Waves across crown
                            float hairFactor = Mathf.SmoothStep(-0.06f, 0.16f, nx - 0.38f * ny);
                            if (hairFactor > 0.001f)
                            {
                                float hairU = 1.5f * nx - 1.2f * ny;
                                float waves = Mathf.Cos(hairU * 6.5f) * 0.045f;
                                relief += waves * (hairFactor * hairFactor) * b;
                            }

                            h += relief * (config.reliefStrength / 1.6f);
                        }
                    }
                    else
                    {
                        // ====================================================
                        // TAILS: Upward Sweeping Wings & Sharp Facet Geometry
                        // ====================================================
                        float tb = blurTails[lx, y];
                        if (tb > 0.001f)
                        {
                            float absX = Mathf.Abs(nx);
                            float relief = Mathf.Pow(tb, 0.50f) * 0.44f;

                            // 1. Central Diamond Spine with razor-sharp central ridge
                            if (absX < 0.095f && ny >= -0.42f && ny <= 0.42f)
                            {
                                float wCore = ny > 0.05f ? 0.088f * (0.42f - ny) / 0.37f : 0.088f * (ny + 0.42f) / 0.47f;
                                if (absX <= wCore && wCore > 0.001f)
                                {
                                    float facet = 1.0f - absX / wCore;
                                    float coreVol = facet * 0.40f * Mathf.Pow(Mathf.Sin(Mathf.PI * (ny + 0.42f) / 0.84f), 0.65f);
                                    relief = Mathf.Max(relief, coreVol);
                                }
                            }

                            // 2. Feather Blade 3D Facet Ridges (Dual-facet light reflection)
                            if (absX > 0.04f)
                            {
                                // Blade 1 spine: upward curve to (0.60, 0.62)
                                float b1TargetY = 0.11f + 1.08f * Mathf.Pow(Mathf.Max(0f, absX - 0.08f), 1.02f);
                                float b1Dist = Mathf.Abs(ny - b1TargetY);
                                if (b1Dist < 0.12f && absX < 0.60f)
                                {
                                    float b1Ridge = (1.0f - b1Dist / 0.12f) * 0.24f * (1.0f - absX / 0.62f);
                                    relief += b1Ridge;
                                }

                                // Blade 2 spine: upward curve to (0.66, 0.40)
                                float b2TargetY = 0.02f + 0.66f * (absX - 0.08f);
                                float b2Dist = Mathf.Abs(ny - b2TargetY);
                                if (b2Dist < 0.11f && absX < 0.66f)
                                {
                                    float b2Ridge = (1.0f - b2Dist / 0.11f) * 0.22f * (1.0f - absX / 0.68f);
                                    relief += b2Ridge;
                                }

                                // Blade 3 spine: curve to (0.58, 0.16)
                                float b3TargetY = -0.07f + 0.46f * (absX - 0.07f);
                                float b3Dist = Mathf.Abs(ny - b3TargetY);
                                if (b3Dist < 0.09f && absX < 0.58f)
                                {
                                    float b3Ridge = (1.0f - b3Dist / 0.09f) * 0.19f * (1.0f - absX / 0.60f);
                                    relief += b3Ridge;
                                }

                                // Blade 4 spur: curve to (0.40, -0.06)
                                float b4TargetY = -0.16f + 0.30f * (absX - 0.04f);
                                float b4Dist = Mathf.Abs(ny - b4TargetY);
                                if (b4Dist < 0.07f && absX < 0.40f)
                                {
                                    float b4Ridge = (1.0f - b4Dist / 0.07f) * 0.15f * (1.0f - absX / 0.42f);
                                    relief += b4Ridge;
                                }
                            }

                            h += relief * (config.reliefStrength / 1.6f);
                        }
                    }

                    heightField[x, y] = Mathf.Clamp01(h);
                }
            }

            // 4. Compute Normal Map & Ambient Occlusion Map
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

            float strength = 16.0f * (config.reliefStrength / 1.6f);

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

                    byte rByte = (byte)Mathf.Clamp((n.x * 0.5f + 0.5f) * 255.0f, 0f, 255f);
                    byte gByte = (byte)Mathf.Clamp((n.y * 0.5f + 0.5f) * 255.0f, 0f, 255f);
                    byte bByte = (byte)Mathf.Clamp((n.z * 0.5f + 0.5f) * 255.0f, 0f, 255f);

                    int idx = y * width + x;
                    normalPixels[idx] = new Color32(rByte, gByte, bByte, 255);

                    float slope = Mathf.Sqrt(dx * dx + dy * dy);
                    float hVal = heightField[x, y];
                    float ao = Mathf.Clamp01(1.0f - (slope * 0.22f + (1.0f - hVal) * 0.05f));
                    byte aoByte = (byte)(ao * 255.0f);
                    aoPixels[idx] = new Color32(aoByte, aoByte, aoByte, 255);
                }
            }

            normalMap.SetPixels32(normalPixels);
            normalMap.Apply(true, false);

            aoMap.SetPixels32(aoPixels);
            aoMap.Apply(true, false);
        }

        private static float C1Dome(float dist, float radius)
        {
            if (dist >= radius) return 0f;
            float u = dist / radius;
            return (1.0f - u * u) * (1.0f - u * u);
        }

        private static float[,] BlurSeparable(float[,] src, int w, int h, int radius)
        {
            float[,] temp = new float[w, h];
            float[,] dst = new float[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = Mathf.Clamp(x + k, 0, w - 1);
                        sum += src[sx, y];
                        count++;
                    }
                    temp[x, y] = sum / count;
                }
            }

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sy = Mathf.Clamp(y + k, 0, h - 1);
                        sum += temp[x, sy];
                        count++;
                    }
                    dst[x, y] = sum / count;
                }
            }

            return dst;
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
    }
}
