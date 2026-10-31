using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Material library and shared assets. The asset Assets/_Game/Settings/RingAssets.asset is written by
    /// BatchTools.RebuildAll from every IRingMaterialDefs implementation (editor), and referenced by GameBootstrap,
    /// so all materials (and their shaders) are included in the player.
    /// </summary>
    [CreateAssetMenu(menuName = "Megastructure/RingAssets")]
    public sealed class RingAssets : ScriptableObject
    {
        [Serializable]
        public struct Entry { public string id; public Material material; }

        public List<Entry> materials = new List<Entry>();
        public UnityEngine.UIElements.PanelSettings panelSettings;
        public Font font;
        public ShaderVariantCollection shaderVariants;
        public Mesh skyDomeMesh;

        Dictionary<string, Material> _map;

        public static RingAssets Instance { get; set; }

        /// <summary>Material by id (see <see cref="RingMat"/>). Logs an error and returns null if it is missing.</summary>
        void EnsureMap()
        {
            if (_map != null) return;
            _map = new Dictionary<string, Material>();
            foreach (var e in materials) if (e.material != null && !string.IsNullOrEmpty(e.id)) _map[e.id] = e.material;
        }

        public Material Get(string id)
        {
            EnsureMap();
            if (_map.TryGetValue(id, out var m)) return m;
            Debug.LogError("[RingAssets] missing material id '" + id + "'");
            return null;
        }

        public bool Has(string id)
        {
            EnsureMap();
            return _map.ContainsKey(id);
        }
    }

    /// <summary>Material ids (asset name = M_&lt;id&gt;.mat). Every id is defined by exactly one IRingMaterialDefs owner.</summary>
    public static class RingMat
    {
        // lead / rendering foundations
        public const string Sky = "Sky";
        public const string Ceramic = "Ceramic";             // M01 CeramicWhite
        public const string CeramicShade = "CeramicShade";   // M01 variant, recesses and joints
        public const string PaleGold = "PaleGold";           // M01 variant, trim and frames
        public const string Metal = "Metal";                 // capsule interior metal, rails
        public const string Emissive = "Emissive";           // M12 flat HDR emissive (WarmLamp)
        public const string EmissiveAmber = "EmissiveAmber"; // M12 tunnel markers / strips
        public const string Mote = "Mote";                   // M13 additive motes
        public const string LightPool = "LightPool";         // additive light pools (radius 3 m)
        public const string Glow = "Glow";                   // additive glow quads (engines, lamps)
        public const string FinalDither = "FinalDither";     // full-screen final static dither (H5)
        // terrain and valley
        public const string Terrain = "Terrain";             // M02
        public const string Water = "Water";                 // M03
        public const string Grass = "Grass";                 // M04 (indirect)
        public const string Flower = "Flower";               // M04 variant (indirect)
        public const string PathStrip = "PathStrip";         // path strip and kerbs
        public const string Stone = "Stone";                 // footbridge vault, apron
        // structures
        public const string Wall = "Wall";                   // M06
        public const string ArkHull = "ArkHull";             // M07
        public const string ArkWindow = "ArkWindow";         // M07 windows (footprint fade)
        public const string Tunnel = "Tunnel";               // M08
        public const string Hull = "Hull";                   // M09 (outside clipmap)
        public const string OuterFace = "OuterFace";         // wall outer face (beat 7)
        public const string SwarmPanel = "SwarmPanel";       // M10 (indirect)
        public const string Deck = "Deck";                   // platform deck (ceramic, 2 m tiles)
        public const string Beacon = "Beacon";               // beacon lens (HDR x8)
    }

    /// <summary>World sets toggled by the BeatDirector (renderer.enabled only; never Instantiate during the walk).</summary>
    public enum RingSet { Always = 0, FarValley = 1, NearValley = 2, NearPlain = 3, Tunnel = 4, Outside = 5, Ark = 6, Capsule = 7 }
}
