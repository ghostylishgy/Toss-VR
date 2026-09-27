using System;
using UnityEngine;

namespace Toss.Coin
{
    public static class CoinTextureGenerator
    {
        // === HEADS: Iconic Classical Greco-Roman Profile (Pointing Left) ===
        private static readonly Vector2[] HeadsProfile = new Vector2[]
        {
            new Vector2( 0.04f,  0.55f), // Top crown apex
            new Vector2(-0.10f,  0.52f),
            new Vector2(-0.20f,  0.44f), // Hairline apex
            new Vector2(-0.26f,  0.34f), // Forehead
            new Vector2(-0.31f,  0.22f), // Brow ridge
            new Vector2(-0.28f,  0.17f), // Nasion indentation
            new Vector2(-0.35f,  0.08f), // Upper nose bridge
            new Vector2(-0.46f, -0.02f), // Sharp classical nose tip (pointing left)
            new Vector2(-0.36f, -0.05f), // Nostril / columella
            new Vector2(-0.35f, -0.10f), // Upper lip crest
            new Vector2(-0.40f, -0.12f), // Upper lip pout
            new Vector2(-0.33f, -0.14f), // Mouth fissure indent
            new Vector2(-0.38f, -0.17f), // Lower lip pout
            new Vector2(-0.31f, -0.20f), // Mentolabial fold indent
            new Vector2(-0.39f, -0.28f), // Chin apex (strong classical chin)
            new Vector2(-0.34f, -0.36f), // Chin under-curve
            new Vector2(-0.24f, -0.38f), // Submandibular line
            new Vector2(-0.18f, -0.42f), // Throat curve
            new Vector2(-0.18f, -0.52f), // Front neck
            new Vector2(-0.24f, -0.62f), // Front bust truncation corner
            new Vector2(-0.10f, -0.66f), // Classical curved truncation bottom
            new Vector2( 0.08f, -0.65f),
            new Vector2( 0.24f, -0.58f), // Rear truncation corner
            new Vector2( 0.22f, -0.40f), // Back neck
            new Vector2( 0.26f, -0.26f), // Nape
            new Vector2( 0.38f, -0.18f), // Chignon bun lower contour
            new Vector2( 0.46f, -0.06f), // Bun rear apex
            new Vector2( 0.44f,  0.08f), // Bun upper contour
            new Vector2( 0.36f,  0.20f), // Bun crown junction
            new Vector2( 0.30f,  0.34f), // Rear crown
            new Vector2( 0.20f,  0.46f),
            new Vector2( 0.12f,  0.52f)
        };

        private static readonly Vector2[] HeadsHair = new Vector2[]
        {
            new Vector2(-0.20f,  0.44f),
            new Vector2(-0.10f,  0.52f),
            new Vector2( 0.04f,  0.55f),
            new Vector2( 0.12f,  0.52f),
            new Vector2( 0.20f,  0.46f),
            new Vector2( 0.30f,  0.34f),
            new Vector2( 0.36f,  0.20f),
            new Vector2( 0.44f,  0.08f),
            new Vector2( 0.46f, -0.06f),
            new Vector2( 0.38f, -0.18f),
            new Vector2( 0.26f, -0.26f),
            new Vector2( 0.14f, -0.16f),
            new Vector2( 0.02f, -0.02f),
            new Vector2(-0.06f,  0.16f),
            new Vector2(-0.14f,  0.32f)
        };

        // === TAILS: Symmetrical Heraldic Spread-Wing Eagle ===
        private static readonly Vector2[] EagleHalf = new Vector2[]
        {
            new Vector2(0.00f,  0.16f), // Neck / chest top
            new Vector2(0.10f,  0.20f), // Shoulder inner
            new Vector2(0.24f,  0.28f), // Wing arch root
            new Vector2(0.42f,  0.38f), // Wing curve upward
            new Vector2(0.60f,  0.46f), // Primary Feather 1 Arch
            new Vector2(0.76f,  0.46f), // Primary Feather 1 Tip (sweeping up & out)
            new Vector2(0.68f,  0.36f), // Notch 1
            new Vector2(0.80f,  0.30f), // Primary Feather 2 Tip (broad horizontal reach)
            new Vector2(0.68f,  0.22f), // Notch 2
            new Vector2(0.74f,  0.12f), // Primary Feather 3 Tip
            new Vector2(0.62f,  0.06f), // Notch 3
            new Vector2(0.66f, -0.02f), // Feather 4 Tip
            new Vector2(0.54f, -0.06f), // Notch 4
            new Vector2(0.56f, -0.12f), // Feather 5 Tip
            new Vector2(0.42f, -0.16f), // Lower wing border
            new Vector2(0.28f, -0.18f), // Flank / leg joint
            new Vector2(0.26f, -0.32f), // Tail outer feather tip
            new Vector2(0.18f, -0.36f), // Tail notch 1
            new Vector2(0.14f, -0.46f), // Tail feather 2 tip
            new Vector2(0.08f, -0.44f), // Tail notch 2
            new Vector2(0.00f, -0.52f)  // Central tail apex
        };

        private static readonly Vector2[] EagleHead = new Vector2[]
        {
            new Vector2( 0.04f,  0.16f), // Neck right
            new Vector2( 0.08f,  0.26f), // Nape
            new Vector2( 0.06f,  0.36f), // Rear crest
            new Vector2( 0.00f,  0.42f), // Crest top
            new Vector2(-0.10f,  0.42f), // Crown
            new Vector2(-0.18f,  0.36f), // Brow overhang
            new Vector2(-0.28f,  0.30f), // Upper beak ridge
            new Vector2(-0.32f,  0.23f), // Hooked beak sharp downward tip
            new Vector2(-0.25f,  0.23f), // Under-hook
            new Vector2(-0.18f,  0.25f), // Gape / mouth slit
            new Vector2(-0.12f,  0.18f), // Throat to breast
            new Vector2( 0.00f,  0.16f)
        };

        private static readonly Vector2[] ShieldHalf = new Vector2[]
        {
            new Vector2(0.00f,  0.14f),
            new Vector2(0.16f,  0.14f),
            new Vector2(0.16f, -0.04f),
            new Vector2(0.10f, -0.18f),
            new Vector2(0.00f, -0.28f)
        };

        private static readonly Vector2[] BranchHalf = new Vector2[]
        {
            new Vector2(0.00f, -0.20f),
            new Vector2(0.14f, -0.20f),
            new Vector2(0.28f, -0.22f),
            new Vector2(0.40f, -0.28f),
            new Vector2(0.44f, -0.32f),
            new Vector2(0.38f, -0.33f),
            new Vector2(0.24f, -0.27f),
            new Vector2(0.12f, -0.25f),
            new Vector2(0.00f, -0.25f)
        };

        public static void GenerateTextures(CoinVisualConfig config, out Texture2D normalMap, out Texture2D aoMap, int width = 1024, int height = 512)
        {
            if (config == null) config = new CoinVisualConfig();

            int halfW = width / 2;

            // 1. Rasterize binary masks
            float[,] maskHeads = new float[halfW, height];
            float[,] maskHair = new float[halfW, height];

            float[,] maskWings = new float[halfW, height];
            float[,] maskHead = new float[halfW, height];
            float[,] maskShield = new float[halfW, height];
            float[,] maskBranch = new float[halfW, height];

            for (int y = 0; y < height; y++)
            {
                float ny = (y - height * 0.5f) / (height * 0.5f); // [-1, 1]

                for (int x = 0; x < halfW; x++)
                {
                    float nx = (x - halfW * 0.5f) / (halfW * 0.5f); // [-1, 1]
                    Vector2 p = new Vector2(nx, ny);

                    // Heads masks
                    if (PointInPolygon(p, HeadsProfile)) maskHeads[x, y] = 1.0f;
                    if (PointInPolygon(p, HeadsHair)) maskHair[x, y] = 1.0f;

                    // Tails masks (mirrored on X for symmetry)
                    Vector2 pSym = new Vector2(Mathf.Abs(nx), ny);
                    if (PointInPolygon(pSym, EagleHalf)) maskWings[x, y] = 1.0f;
                    if (PointInPolygon(p, EagleHead)) maskHead[x, y] = 1.0f;
                    if (PointInPolygon(pSym, ShieldHalf)) maskShield[x, y] = 1.0f;
                    if (PointInPolygon(pSym, BranchHalf)) maskBranch[x, y] = 1.0f;
                }
            }

            // 2. Multi-pass separable box-blur to produce smooth minted bas-relief bevels
            float[,] blurHeads = BlurSeparable(maskHeads, halfW, height, 4);
            blurHeads = BlurSeparable(blurHeads, halfW, height, 3);

            float[,] blurHair = BlurSeparable(maskHair, halfW, height, 3);

            float[,] blurWings = BlurSeparable(maskWings, halfW, height, 4);
            blurWings = BlurSeparable(blurWings, halfW, height, 3);

            float[,] blurHead = BlurSeparable(maskHead, halfW, height, 3);
            float[,] blurShield = BlurSeparable(maskShield, halfW, height, 2);
            float[,] blurBranch = BlurSeparable(maskBranch, halfW, height, 3);

            // 3. Assemble Heightfield for the entire 1024x512 texture
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

                    if (r > 0.98f)
                    {
                        heightField[x, y] = 0.0f;
                        continue;
                    }

                    // Basin floor
                    float h = 0.16f;

                    // Raised stepped inner rim at r in [0.82, 0.88]
                    if (r >= 0.82f && r <= 0.88f)
                    {
                        float rimDist = Mathf.Abs(r - 0.85f);
                        float rimT = Mathf.Clamp01(1.0f - (rimDist / 0.03f));
                        h += rimT * 0.22f;
                    }

                    // Subtle silky brushed texture (85% noise reduction)
                    float theta = Mathf.Atan2(ny, nx);
                    float brushScale = config.brushedGrainStrength / 0.025f;
                    float subtleBrush = (Mathf.Sin(r * 24.0f) * 0.0015f + Mathf.Cos(theta * 16.0f) * 0.0015f) * brushScale;
                    h += subtleBrush;

                    if (!isTails)
                    {
                        // === HEADS ===
                        float b = blurHeads[lx, y];
                        if (b > 0.001f)
                        {
                            float relief = Mathf.Pow(b, 0.55f) * 0.58f;

                            // Hair volume layer
                            float hb = blurHair[lx, y];
                            if (hb > 0.01f)
                            {
                                relief += Mathf.Pow(hb, 0.70f) * 0.12f;
                                float waves = Mathf.Sin((nx * 5.0f - ny * 7.0f) * Mathf.PI) * 0.035f;
                                relief += Mathf.Max(0.0f, waves) * hb;
                            }
                            else
                            {
                                // Cheek fullness
                                float cheekD = Mathf.Sqrt((nx + 0.12f) * (nx + 0.12f) + (ny + 0.02f) * (ny + 0.02f));
                                if (cheekD < 0.16f)
                                {
                                    relief += (1.0f - cheekD / 0.16f) * 0.05f;
                                }
                            }

                            // Eye & Brow arch accent
                            float eyeD = Mathf.Sqrt((nx + 0.16f) * (nx + 0.16f) + (ny - 0.18f) * (ny - 0.18f));
                            if (eyeD < 0.04f)
                            {
                                relief += (1.0f - eyeD / 0.04f) * 0.05f;
                            }

                            h += relief * (config.reliefStrength / 1.6f);
                        }
                    }
                    else
                    {
                        // === TAILS ===
                        float relief = 0.0f;

                        // Symmetrical Wings & Tail
                        float wb = blurWings[lx, y];
                        if (wb > 0.001f)
                        {
                            float wingRelief = Mathf.Pow(wb, 0.55f) * 0.52f;
                            // Crisp primary feather separation grooves
                            if (Mathf.Abs(nx) > 0.22f)
                            {
                                float featherGrooves = Mathf.Sin((ny * 13.0f + Mathf.Abs(nx) * 3.0f) * Mathf.PI);
                                if (featherGrooves < -0.65f)
                                {
                                    wingRelief -= 0.08f;
                                }
                            }
                            relief = Mathf.Max(relief, wingRelief);
                        }

                        // Laurel / Olive Branch Pedestal
                        float bb = blurBranch[lx, y];
                        if (bb > 0.001f)
                        {
                            float branchRelief = 0.35f + Mathf.Pow(bb, 0.60f) * 0.18f;
                            relief = Mathf.Max(relief, branchRelief);
                        }

                        // Eagle Head (Hooked Beak, Turned Left)
                        float hb = blurHead[lx, y];
                        if (hb > 0.001f)
                        {
                            float headRelief = 0.42f + Mathf.Pow(hb, 0.55f) * 0.24f;
                            // Eye
                            float eyeD = Mathf.Sqrt((nx + 0.10f) * (nx + 0.10f) + (ny - 0.32f) * (ny - 0.32f));
                            if (eyeD < 0.026f) headRelief += 0.07f;
                            if (eyeD < 0.012f) headRelief -= 0.06f;
                            relief = Mathf.Max(relief, headRelief);
                        }

                        // Central Heraldic Shield
                        float sb = blurShield[lx, y];
                        if (sb > 0.001f)
                        {
                            float shieldRelief = 0.48f + Mathf.Pow(sb, 0.55f) * 0.24f;
                            if (ny < 0.04f)
                            {
                                float stripe = Mathf.Sin(nx * 32.0f * Mathf.PI);
                                if (stripe > 0.0f) shieldRelief += 0.045f;
                            }
                            else
                            {
                                shieldRelief += 0.035f; // Chief horizontal band
                            }
                            relief = Mathf.Max(relief, shieldRelief);
                        }

                        h += relief * (config.reliefStrength / 1.6f);
                    }

                    heightField[x, y] = Mathf.Clamp01(h);
                }
            }

            // 4. Compute Normal Map & AO Map
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

            float strength = 14.0f * (config.reliefStrength / 1.6f);

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
                    float ao = Mathf.Clamp01(1.0f - (slope * 0.24f + (1.0f - hVal) * 0.06f));
                    byte aoByte = (byte)(ao * 255.0f);
                    aoPixels[idx] = new Color32(aoByte, aoByte, aoByte, 255);
                }
            }

            normalMap.SetPixels32(normalPixels);
            normalMap.Apply(true, false);

            aoMap.SetPixels32(aoPixels);
            aoMap.Apply(true, false);
        }

        private static float[,] BlurSeparable(float[,] src, int w, int h, int radius)
        {
            float[,] temp = new float[w, h];
            float[,] dst = new float[w, h];

            // Horizontal pass
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

            // Vertical pass
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
