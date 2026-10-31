using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 2, far-layer staging with light (critique R2-07 (1); an addition to the 14.2 beat-2/3 rig, no real lights).
    ///
    /// In KS2a the far layers barely separated: C3 (480 m) stood about 6 levels below the far wall (0.124 against 0.149), a
    /// separation the logged R1-06(b) calibration shows fog alone cannot raise. Two of the cold service lamps on the -30 belt
    /// of face 0 (Beat02_Bridge.Bolt, lamps 1 and 2, 123 and 210 m along the face from the bolt lamp) stand behind C3 as seen
    /// from the bridge (bearings about 24 and 18 degrees from KS2a; C3 covers 16-25). Each is a twin flood: one head throws a
    /// wash down the wall below the belt, the other up the wall above it. Each wash is one camera-facing Glow pool (true
    /// diameter <see cref="FAR_POOL_D"/>, centred <see cref="FAR_POOL_DROP"/> below or <see cref="FAR_POOL_RISE"/> above the
    /// lamp and <see cref="FAR_POOL_STANDOFF"/> off the wall, additive, fogged, soft-depth faded: the Glow quad is pulled half
    /// its diameter (80 m) toward the camera, which keeps it behind C3's silhouette edge from every point of the bridge (6 m
    /// margin from the landing end, Beat02Tests), so it never draws over the column). Together the four pools light the wall
    /// behind C3 from about y -130 to +130 at half maximum (KS2a rows about 0.15-0.55), so C3
    /// stands as a dark silhouette against a washed wall (target at least 10 levels below its background, R2-07). C4 is not
    /// staged: it stands behind C1 from every point of the bridge. The pools belong to the bridge staging and switch off with
    /// the skeleton (d 380, inside the hall, unseen); the lamps stay.
    /// </summary>
    public sealed partial class Beat02_Bridge
    {
        /// <summary>True diameter of each wash pool (Glow contract: the quad spans twice this; half maximum at about 0.37 of it from the centre).</summary>
        public const float FAR_POOL_D = 160f;
        /// <summary>Centre of the down wash below its lamp.</summary>
        public const float FAR_POOL_DROP = 40f;
        /// <summary>Centre of the up wash above its lamp.</summary>
        public const float FAR_POOL_RISE = 100f;
        /// <summary>Pool centre off the wall plane (apothem 350 - 4).</summary>
        public const float FAR_POOL_STANDOFF = 4f;
        /// <summary>
        /// Peak strength (COLD_LAMP x strength before fog; about 0.27 transmittance at 620 m in the beat-2 fog). Look-dev
        /// lead_b01: 0.08 added only +4-6 levels at a pool's peak, under the 10-level target.
        /// </summary>
        public const float FAR_POOL_STRENGTH = 0.18f;

        InstancedBatch _farPools;

        /// <summary>Centre of the down (<paramref name="up"/> false) or up wash of belt lamp i (1 or 2).</summary>
        public static Vector3 FarPoolCentre(int lamp, bool up)
        {
            Vector3 head = BeltLampHead(lamp);
            Vector2 n = MeshKit.HexFaceNormal(BELT_LAMP_FACE);
            float dn = AbyssConst.SHAFT_APOTHEM - FAR_POOL_STANDOFF - (AbyssConst.BELT_INNER_APOTHEM - BELT_LAMP_STANDOFF);
            return head + new Vector3(n.x * dn, up ? FAR_POOL_RISE : -FAR_POOL_DROP, n.y * dn);
        }

        void BuildFarLayer(BeatContext ctx)
        {
            if (_tier == BeatTier.Graybox) return;
            Material mat = GlowMaterial(ctx.Mats, "b2farpool", AbyssPalette.COLD_LAMP, FAR_POOL_STRENGTH, 0f);
            _farPools = new InstancedBatch("B2_FarPools", _glowQuad, mat, ShadowCastingMode.Off, false, 0, 1000f);
            for (int i = 1; i < BeltLampCount; i++)
            {
                _farPools.Add(Matrix4x4.TRS(FarPoolCentre(i, false), Quaternion.identity, Vector3.one * FAR_POOL_D));
                _farPools.Add(Matrix4x4.TRS(FarPoolCentre(i, true), Quaternion.identity, Vector3.one * FAR_POOL_D));
            }
            _batches.Add(_farPools);
        }
    }
}
