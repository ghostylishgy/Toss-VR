using System;
using UnityEngine;

namespace Toss.Coin
{
    [Serializable]
    public class CoinVisualConfig
    {
        [Header("Geometry Dimensions (Meters)")]
        [Tooltip("Coin diameter in meters. Default 30mm (0.030m), adjustable 28-32mm.")]
        [Range(0.025f, 0.035f)]
        public float diameter = 0.030f;

        [Tooltip("Coin thickness in meters. Default 2.4mm (0.0024m), adjustable 2.2-2.6mm.")]
        [Range(0.0018f, 0.0035f)]
        public float thickness = 0.0024f;

        [Tooltip("Number of stylized ridges along the reeded edge. Default 48.")]
        [Range(24, 96)]
        public int edgeRidgeCount = 48;

        [Tooltip("Depth of ridge indentations in meters.")]
        [Range(0.0001f, 0.0006f)]
        public float edgeRidgeDepth = 0.0003f;

        [Tooltip("Width of the raised protective outer rim in meters.")]
        [Range(0.0008f, 0.0025f)]
        public float rimWidth = 0.0015f;

        [Tooltip("Height of the raised rim above the recessed face basin in meters.")]
        [Range(0.0001f, 0.0005f)]
        public float rimHeight = 0.00025f;

        [Header("Relief Depth & Graphics")]
        [Tooltip("Virtual bas-relief depth multiplier for normal map.")]
        [Range(0.5f, 3.0f)]
        public float reliefStrength = 1.6f;

        [Tooltip("Subtle radial brushed metal grain intensity.")]
        [Range(0.0f, 0.1f)]
        public float brushedGrainStrength = 0.025f;

        [Header("Material PBR (Silver Metallic Baseline)")]
        [Tooltip("Silver alloy tint. Neutral, cool satin silver. Prohibited: gold, bronze, chrome.")]
        public Color metalTint = new Color(0.88f, 0.89f, 0.91f, 1.0f);

        [Tooltip("Metallic level. Highly metallic, but not pure dielectric.")]
        [Range(0.85f, 1.0f)]
        public float metallic = 0.96f;

        [Tooltip("Surface roughness. Moderate roughness (0.35 - 0.45) for satin / brushed finish. No mirror chrome.")]
        [Range(0.25f, 0.60f)]
        public float metalRoughness = 0.42f;

        [Tooltip("Smoothness on ridges and rim where edge highlights catch the light.")]
        [Range(0.40f, 0.75f)]
        public float edgeHighlightSmoothness = 0.62f;

        [Header("Inspection & Observation Parameters")]
        [Tooltip("Default comfortable viewing distance in meters in front of VR Glasses eyes (0.35m - 0.45m).")]
        [Range(0.25f, 0.60f)]
        public float defaultObservationDistance = 0.40f;

        [Tooltip("Default tilted presentation angle for natural three-quarter dimensional reading.")]
        public Vector3 defaultDisplayAngle = new Vector3(15f, -20f, 0f);

        [Tooltip("Turntable continuous flip rotation speed in degrees per second.")]
        [Range(10f, 180f)]
        public float turntableRotationSpeed = 36.0f; // ~10 seconds per 360° flip

        // Helper to get Unity smoothness (1 - roughness)
        public float FaceSmoothness => Mathf.Clamp01(1.0f - metalRoughness);
    }
}
