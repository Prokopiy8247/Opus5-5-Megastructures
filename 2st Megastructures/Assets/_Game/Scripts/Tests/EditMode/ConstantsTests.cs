using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>The constants table of 14.12 and the derived numbers quoted elsewhere in Part 14.</summary>
    public class ConstantsTests
    {
        const float Tol = 0.02f;

        [Test]
        public void Ring_Star_Player()
        {
            Assert.AreEqual(16000f, RingConstants.RingR); Assert.AreEqual(32000f, RingConstants.ZenithRange);
            Assert.AreEqual(2f * RingConstants.RingR, RingConstants.ZenithRange);
            Assert.AreEqual(-68f, RingConstants.StarAzimuth); Assert.AreEqual(26f, RingConstants.StarElevation);
            Assert.AreEqual(2.4f, RingConstants.StarDiameterDeg); Assert.AreEqual(1.2f, RingConstants.StarRadiusDeg);
            Assert.AreEqual(96f, RingConstants.StarOutYaw); Assert.AreEqual(7f, RingConstants.StarOutElevation);
            Assert.AreEqual(838f, RingConstants.StarOutRange * Mathf.Tan(RingConstants.StarRadiusDeg * Mathf.Deg2Rad), 1f, "star disc 838 m at 40 km");
            Assert.AreEqual(1.7f, RingConstants.Eye); Assert.AreEqual(1.5f, RingConstants.Walk); Assert.AreEqual(2.5f, RingConstants.Brisk);
            Assert.AreEqual(0.2f, RingConstants.Accel); Assert.AreEqual(0.35f, RingConstants.PlayerRadius);
            Assert.AreEqual(0.75f, RingConstants.StrideWalk); Assert.AreEqual(0.85f, RingConstants.StrideBrisk);
            Assert.AreEqual(1.8f, RingConstants.PlayerHeight); Assert.AreEqual(0.3f, RingConstants.StepOffset); Assert.AreEqual(30f, RingConstants.SlopeLimit);
            Assert.AreEqual(0.05f, RingConstants.NearPlane);
            Assert.LessOrEqual(RingConstants.HeadBob, 0.015f, "head bob at most 1.5 cm (Part 9)");
        }

        [Test]
        public void Beats_Waypoints()
        {
            CollectionAssert.AreEqual(new float[] { 0, 24, 276, 411, 594, 774, 894, 960 }, RingConstants.BeatS);
            CollectionAssert.AreEqual(new float[] { 16, 168, 90, 122, 120, 80, 44 }, RingConstants.BeatT);
            Assert.AreEqual(959f, RingConstants.GoalS);
            var w = new[] { new Vector2(0, 0), new Vector2(0, 24), new Vector2(0, 276), new Vector2(0, 411), new Vector2(0, 558), new Vector2(0, 576),
                            new Vector2(18, 576), new Vector2(100, 576), new Vector2(100, 660), new Vector2(114, 660), new Vector2(234, 660), new Vector2(300, 660) };
            Assert.AreEqual(w.Length, RingConstants.Waypoints.Length);
            for (int i = 0; i < w.Length; i++) Assert.AreEqual(w[i], RingConstants.Waypoints[i], "waypoint " + i);
            Assert.AreEqual(7, RingConstants.BeatNames.Length);
        }

        [Test]
        public void Capsule_Porthole_Hatch()
        {
            Assert.AreEqual(2.6f, RingConstants.CapsuleInnerW); Assert.AreEqual(2.5f, RingConstants.CapsuleInnerH);
            Assert.AreEqual(11.0f, RingConstants.CapsuleZ1 - RingConstants.CapsuleZ0, Tol);
            Assert.AreEqual(0.9f, RingConstants.CapsuleFloorY); Assert.AreEqual(3.4f, RingConstants.CapsuleCeilY);
            Assert.AreEqual(RingConstants.CapsuleInnerH, RingConstants.CapsuleCeilY - RingConstants.CapsuleFloorY, 1e-4f);
            Assert.AreEqual(3.2f, RingConstants.CapsuleInnerW + 2f * RingConstants.CapsuleWall, Tol);
            Assert.AreEqual(3.1f, RingConstants.CapsuleInnerH + 2f * RingConstants.CapsuleWall, Tol);
            Assert.AreEqual(11.8f, RingConstants.HatchZ1 - RingConstants.CapsuleZ0 + RingConstants.CapsuleWall, Tol, "outside length 11.8 (rear wall z -1.8)");
            Assert.AreEqual(new Vector3(-1.46f, 2.98f, 5.65f), RingConstants.PortholeCentre);
            Assert.AreEqual(0.55f, RingConstants.PortholeOpening); Assert.AreEqual(0.80f, RingConstants.PortholeFrame);
            Assert.AreEqual(2.0f, RingConstants.HatchW); Assert.AreEqual(1.9f, RingConstants.HatchH);
            Assert.AreEqual(6.5f, RingConstants.HatchTriggerS); Assert.AreEqual(2.0f, RingConstants.HatchOpenTime);
            // KS01 eye is 0.9 m from the porthole centre along its axis (yaw -45, pitch +25)
            var k = KeyShots.All[1];
            Vector3 d = RingConstants.PortholeCentre - k.Eye;
            Assert.AreEqual(0.9f, d.magnitude, 0.02f, "KS01 distance to the porthole");
            Vector3 axis = RingConstants.DirFromAzEl(RingConstants.PortholeYaw, RingConstants.PortholePitch);
            Assert.Greater(Vector3.Dot(d.normalized, axis), Mathf.Cos(1.5f * Mathf.Deg2Rad), "KS01 on the porthole axis");
            Assert.AreEqual(0.03f, RingConstants.PadTopY); Assert.AreEqual(24f, RingConstants.PadZ0); Assert.AreEqual(30f, RingConstants.PadZ1);
        }

        [Test]
        public void Bridge_River_Clipmap()
        {
            Assert.AreEqual(120f, RingConstants.BridgeZ0); Assert.AreEqual(180f, RingConstants.BridgeZ1); Assert.AreEqual(150f, RingConstants.BridgeCrestZ);
            Assert.AreEqual(3.0f, RingConstants.BridgeCrest); Assert.AreEqual(36f, RingConstants.BridgeSpan); Assert.AreEqual(5.0f, RingConstants.BridgeDeckW);
            Assert.AreEqual(1.0f, RingConstants.RailH); Assert.AreEqual(3.2f, RingConstants.LampH);
            Assert.AreEqual(36f, RingConstants.RiverWidth); Assert.AreEqual(-2.2f, RingConstants.RiverSurfaceY); Assert.AreEqual(3.2f, RingConstants.RiverCarve);
            Assert.AreEqual(0.6f, RingConstants.RiverFlow);
            // the carve reaches the waterline (y -2.2) at 18 m from the centre: the river is 36 m wide (ADR-006)
            Assert.AreEqual(-RingConstants.RiverSurfaceY, RingWorld.RiverCarve(0.5f * RingConstants.RiverWidth), 0.01f, "waterline at the half width");
            Assert.AreEqual(RingConstants.RiverCarve, RingWorld.RiverCarve(10f), 1e-4f, "bed depth");
            Assert.AreEqual(0f, RingWorld.RiverCarve(24.6f), 0.01f, "bank top at 24.6 m");
            Assert.AreEqual(9, RingConstants.ClipLevels); Assert.AreEqual(128, RingConstants.ClipQuads); Assert.AreEqual(0.5f, RingConstants.ClipSpacing0);
            float extent = 0.5f * RingConstants.ClipQuads * RingConstants.ClipSpacing0 * Mathf.Pow(2f, RingConstants.ClipLevels - 1);
            Assert.AreEqual(RingConstants.ClipExtent, extent, 1e-3f, "half extent 64 x spacing of the last level");
            Assert.AreEqual(229376, 8 * 24576 + 32768, "clipmap triangle count of 14.5.2");
            Assert.AreEqual(14.67f, RingConstants.ClipEdgeElevation, 1e-3f);
        }

        [Test]
        public void Ark_ShipShadow_CloudShadows()
        {
            Assert.AreEqual(3000f, RingConstants.ArkLength); Assert.AreEqual(420f, RingConstants.ArkBeam); Assert.AreEqual(260f, RingConstants.ArkHeight);
            Assert.AreEqual(1800f, RingConstants.ArkAltitude); Assert.AreEqual(3180f, RingConstants.ArkTrackZ);
            Assert.AreEqual(-9000f, RingConstants.ArkX0); Assert.AreEqual(6000f, RingConstants.ArkX1); Assert.AreEqual(190f, RingConstants.ArkT);
            Assert.AreEqual(285f, RingConstants.ArkD); Assert.AreEqual(RingConstants.ArkT * RingConstants.Walk, RingConstants.ArkD, 1e-3f);
            Assert.AreEqual(78.9f, (RingConstants.ArkX1 - RingConstants.ArkX0) / RingConstants.ArkT, 0.1f, "ark speed");
            Assert.AreEqual(231f, RingConstants.ArkTriggerS);
            Assert.AreEqual(3000f, RingConstants.ShipShadowL); Assert.AreEqual(620f, RingConstants.ShipShadowW); Assert.AreEqual(172f, RingConstants.ShipShadowPenumbra);
            Assert.AreEqual(0.62f, RingConstants.ShipShadowStrength); Assert.AreEqual(new Vector2(3422f, -1383f), RingConstants.ShipShadowOffset);
            Assert.AreEqual(172f, 0.0419f * 4110f, 1f, "penumbra = star angle x ray length");
            Assert.AreEqual(90f, RingConstants.CloudShadowFromS); Assert.AreEqual(900f, RingConstants.CloudShadowL); Assert.AreEqual(350f, RingConstants.CloudShadowW);
            Assert.AreEqual(600f, RingConstants.CloudShadowPenumbra); Assert.AreEqual(0.25f, RingConstants.CloudShadowStrength); Assert.AreEqual(12f, RingConstants.CloudShadowSpeed);
            Assert.AreEqual(396f, RingConstants.FootbridgeS0); Assert.AreEqual(404f, RingConstants.FootbridgeS1);
            Assert.AreEqual(4.2f, RingConstants.FootbridgeW); Assert.AreEqual(3.2f, RingConstants.FootbridgeH); Assert.AreEqual(8.0f, RingConstants.FootbridgeL);
        }

        [Test]
        public void KS04_ShipGeometryInBentSpace()
        {
            // x_c = -2 000 at KS04: azimuth -38.8 (spanning -54..-11), elevation 32, range 3.78 km in bent space (14.5.3)
            var k = KeyShots.All[4];
            Vector3 Bent(float x) => RingBend.Bend(new Vector3(x, RingConstants.ArkAltitude, RingConstants.ArkTrackZ), k.Eye) - k.Eye;
            Vector3 c = Bent(-2000f);
            Assert.AreEqual(-38.8f, Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg, 1f, "ship azimuth");
            Assert.AreEqual(32f, Mathf.Atan2(c.y, new Vector2(c.x, c.z).magnitude) * Mathf.Rad2Deg, 3f, "ship elevation");
            Assert.AreEqual(3780f, c.magnitude, 40f, "ship range");
            Vector3 a = Bent(-2000f - 1500f), b = Bent(-2000f + 1500f);
            Assert.AreEqual(-54f, Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg, 1.5f, "bow azimuth");
            Assert.AreEqual(-11f, Mathf.Atan2(b.x, b.z) * Mathf.Rad2Deg, 1.5f, "stern azimuth");
        }

        [Test]
        public void Sunset_Levee_Underpass()
        {
            Assert.AreEqual(70f, RingConstants.SunsetT); Assert.AreEqual(105f, RingConstants.SunsetD); Assert.AreEqual(RingConstants.SunsetT * RingConstants.Walk, RingConstants.SunsetD, 1e-3f);
            Assert.AreEqual(0.03f, RingConstants.KeyFloor); Assert.AreEqual(0.09f, RingConstants.NightKey);
            Assert.AreEqual(22f, RingConstants.SquareA); Assert.AreEqual(16f, RingConstants.SquareB); Assert.AreEqual(12f, RingConstants.SquareC);
            Assert.AreEqual(8f, RingConstants.SquareRot); Assert.AreEqual(0.12f, RingConstants.SquareEdge);
            Assert.AreEqual(0.0762f, 5.33f / RingConstants.SunsetT, 0.0005f, "square edge speed (deg/s)");
            Assert.AreEqual(70f, RingConstants.TerminatorMax); Assert.AreEqual(6f, RingConstants.TerminatorSoft);
            Assert.AreEqual(-70f, RingConstants.LeveeX0); Assert.AreEqual(18f, RingConstants.LeveeX1); Assert.AreEqual(558f, RingConstants.LeveeZ0); Assert.AreEqual(640f, RingConstants.LeveeZ1);
            Assert.AreEqual(14f, RingConstants.LeveeH);
            Assert.AreEqual(2.8f, RingConstants.UnderpassW); Assert.AreEqual(2.9f, RingConstants.UnderpassH); Assert.AreEqual(18f, RingConstants.UnderpassLeg);
            Assert.AreEqual(576f, RingConstants.SwapS);
            Assert.AreEqual(13f, Mathf.Atan2(RingConstants.LeveeH - RingConstants.Eye, RingConstants.LeveeZ0 - KeyShots.All[6].Eye.z) * Mathf.Rad2Deg, 0.3f, "KS06 levee top elevation");
            Assert.AreEqual(new Vector3(-110f, 0f, 500f), RingConstants.PavilionP2); Assert.AreEqual(11f, RingConstants.PavilionH);
        }

        [Test]
        public void Wall_Tunnel_Platform()
        {
            Assert.AreEqual(114f, RingConstants.WallX); Assert.AreEqual(234f, RingConstants.WallOuterX); Assert.AreEqual(120f, RingConstants.WallThickness);
            Assert.AreEqual(RingConstants.WallThickness, RingConstants.WallOuterX - RingConstants.WallX, 1e-4f);
            Assert.AreEqual(100f, RingConstants.WallSeam); Assert.AreEqual(12000f, RingConstants.WallTop);
            Assert.AreEqual(2.6f, RingConstants.DoorW); Assert.AreEqual(3.0f, RingConstants.DoorH);
            Assert.AreEqual(RingConstants.DoorH, RingConstants.DoorStraight + RingConstants.DoorCrownR, 1e-4f, "door = 1.7 straight + r 1.3 crown");
            Assert.AreEqual(RingConstants.DoorW, 2f * RingConstants.DoorCrownR, 1e-4f);
            Assert.AreEqual(0.8f, RingConstants.DoorRecess); Assert.AreEqual(0.12f, RingConstants.DoorFrame); Assert.AreEqual(0.5f, RingConstants.DoorLintel);
            Assert.AreEqual(111.5f, RingConstants.LaneCapX); Assert.AreEqual(2.5f, RingConstants.WallX - RingConstants.LaneCapX, 1e-4f);
            Assert.AreEqual(120f, RingConstants.TunnelLen); Assert.AreEqual(117f, RingConstants.TunnelRibX0); Assert.AreEqual(6f, RingConstants.TunnelRibPitch);
            Assert.AreEqual(20, RingConstants.TunnelRibs);
            Assert.Less(RingConstants.TunnelRibX0 + RingConstants.TunnelRibPitch * (RingConstants.TunnelRibs - 1), RingConstants.WallOuterX, "last rib inside the wall");
            Assert.AreEqual(1.585f, 2f * Mathf.Atan(0.5f * RingConstants.DoorW / 94f) * Mathf.Rad2Deg, 0.01f, "KS10 mouth angular width at 94 m");
            Assert.AreEqual(94f, RingConstants.WallOuterX - KeyShots.All[10].Eye.x, 1e-3f, "KS10 distance to the mouth");
            Assert.AreEqual(700f, RingConstants.ClimberZ);
            Assert.AreEqual(234f, RingConstants.PlatformX0); Assert.AreEqual(306f, RingConstants.PlatformX1); Assert.AreEqual(654f, RingConstants.PlatformZ0); Assert.AreEqual(666f, RingConstants.PlatformZ1);
            Assert.AreEqual(72f, RingConstants.PlatformX1 - RingConstants.PlatformX0, 1e-4f); Assert.AreEqual(12f, RingConstants.PlatformZ1 - RingConstants.PlatformZ0, 1e-4f);   // ADR-011
            Assert.AreEqual(1.2f, RingConstants.PlatformThick); Assert.AreEqual(10.3f, RingConstants.PlatformLane);
            // KS07: the door is 35 degrees left of the yaw-80 view
            var k7 = KeyShots.All[7];
            float az = Mathf.Atan2(RingConstants.WallX - k7.Eye.x, 660f - k7.Eye.z) * Mathf.Rad2Deg;
            Assert.AreEqual(-35f, Mathf.DeltaAngle(k7.Yaw, az), 0.5f, "KS07 door azimuth offset");
        }

        [Test]
        public void Hull_Swarm_Beacon()
        {
            Assert.AreEqual(30000f, RingConstants.HullR); Assert.AreEqual(200f, RingConstants.HullDepth);   // ADR-011
            float hz = Mathf.Sqrt(2f * RingConstants.HullR * RingConstants.HullDepth + RingConstants.HullDepth * RingConstants.HullDepth);
            Assert.AreEqual(RingConstants.HullHorizon, hz, 5f, "hull horizon");
            Assert.AreEqual(RingConstants.HullDip, Mathf.Acos(RingConstants.HullR / (RingConstants.HullR + RingConstants.HullDepth)) * Mathf.Rad2Deg, 0.1f, "hull dip");
            Assert.AreEqual(new Vector2(300f, 660f), RingConstants.HullCentre);
            int total = 0; foreach (int c in RingConstants.SwarmCounts) total += c;
            Assert.AreEqual(24000, total, "swarm panels");
            for (int i = 0; i < 3; i++)
            {
                float r = RingConstants.SwarmRadii[i];
                Assert.AreEqual(r * r / (RingConstants.SwarmRadii[0] * RingConstants.SwarmRadii[0]),
                                RingConstants.SwarmCounts[i] / (float)RingConstants.SwarmCounts[0], 0.06f * r * r / (RingConstants.SwarmRadii[0] * RingConstants.SwarmRadii[0]), "area ratio 25:64:144");
            }
            float[] ang = { 7.1f, 11.3f, 16.7f };
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(ang[i], Mathf.Atan(RingConstants.SwarmRadii[i] / RingConstants.StarOutRange) * Mathf.Rad2Deg, 0.3f, "band angular radius " + i);
            CollectionAssert.AreEqual(new float[] { 600, 1200, 2200 }, RingConstants.SwarmPeriods);
            CollectionAssert.AreEqual(new float[] { 10, 58, 75 }, RingConstants.SwarmTilt);
            CollectionAssert.AreEqual(new float[] { 20, -35, 75 }, RingConstants.SwarmNode);
            CollectionAssert.AreEqual(new float[] { 1, -1, 1 }, RingConstants.SwarmSense);
            Assert.AreEqual(70f, RingConstants.SwarmPlate); Assert.AreEqual(2.2f, RingConstants.SwarmMinPx); Assert.AreEqual(0.09f, RingConstants.SwarmGlint);
            Assert.AreEqual(6f, RingConstants.SwarmGlintAmp); Assert.AreEqual(8f, RingConstants.SwarmClamp);
            Assert.AreEqual(new Vector2(301f, 660f), RingConstants.BeaconXZ); Assert.AreEqual(1.3f, RingConstants.BeaconH); Assert.AreEqual(0.14f, RingConstants.BeaconLens);
            Assert.AreEqual(4.0f, RingConstants.BeaconPeriod); Assert.AreEqual(905f, RingConstants.BeaconFirstPingS);
        }

        [Test]
        public void Fog_Fov_Far_Slew_Holds_UiTiming()
        {
            // ADR-012: beats 1-3 at 1.0e-4 (was 2.5e-4), the arch haze sigma equals the beat 1-3 sigma
            CollectionAssert.AreEqual(new float[] { 1.0e-4f, 1.0e-4f, 1.0e-4f, 1.0e-4f, 5.0e-4f, 4.0e-3f, 0f }, RingConstants.FogSigma);
            Assert.AreEqual(3.6e-4f, RingConstants.FogSigmaNight4);
            Assert.AreEqual(RingConstants.FogSigma[0], RingConstants.FogSigmaDay);
            Assert.AreEqual(8326f, 0.8326f / RingConstants.FogSigma[0], 2f, "d50 beats 1-3 (no mist)");
            Assert.AreEqual(1665f, 0.8326f / RingConstants.FogSigma[4], 2f, "d50 beat 5");
            Assert.AreEqual(208f, 0.8326f / RingConstants.FogSigma[5], 1f, "d50 beat 6");
            CollectionAssert.AreEqual(new float[] { 60, 64, 66, 64, 66, 60, 60 }, RingConstants.Vfov);
            Assert.AreEqual(15000f, RingConstants.FarDefault); Assert.AreEqual(60000f, RingConstants.FarOutside);
            Assert.AreEqual(0.30f, RingConstants.EvSlew); Assert.AreEqual(6f, RingConstants.WbSlew); Assert.AreEqual(0.1f, RingConstants.BloomSlew);
            float holds = 0; foreach (var h in RingConstants.CinematicHolds) holds += h.y;
            Assert.AreEqual(49f, holds, 1e-3f, "cinematic holds 49 s");
            Assert.AreEqual(8, RingConstants.CinematicHolds.Length);
            Assert.AreEqual(0.6f, RingConstants.FadeInStart); Assert.AreEqual(3.1f, RingConstants.FadeInEnd);
            Assert.AreEqual(5.4f, RingConstants.TitleOut1); Assert.AreEqual(6.0f, RingConstants.HintAt);
            float ending = RingConstants.EndDwell + RingConstants.EndFade + RingConstants.EndBlack + RingConstants.EndTextIn + RingConstants.EndTextHold + RingConstants.EndTextOut;
            Assert.AreEqual(18.5f, ending, 1e-3f, "ending 18.5 s after the goal");
            Assert.AreEqual("THE RING", RingConstants.Title);
            Assert.AreEqual("Someone built all of this to be home.", RingConstants.EndLine);
        }

        [Test]
        public void Palette_HasEveryToken()
        {
            foreach (var t in new[] { "CeramicWhite", "CeramicShade", "HullPale", "PaleGold", "WarmLamp", "AmberLamp", "BeaconAmber", "MeadowA", "MeadowB",
                                      "Wheat", "LeveeGreen", "TealRiver", "WaterDeep", "SkyDayZenith", "SkyDayMid", "SkyDayHorAway", "SkyDayHorToward", "Aureole",
                                      "SkyDuskZenith", "SkyDuskMid", "SkyDuskHorAway", "SkyDuskHorToward", "SkyNightZenith", "SkyNightMid", "SkyNightHor",
                                      "ArchGlow", "CityWarm", "CityCool", "CloudWhite", "StarCore", "StarOutside", "KeyLight", "DuskKey", "ArchFill", "RimFill",
                                      "Practical2700", "NightTriSky", "NightTriEq", "NightTriGround", "ArkHull", "ArkUnderside", "EngineBlue", "WindowWarm",
                                      "SquareSlate", "WallTone", "WallNight", "TunnelGrey", "TunnelFog", "SwarmBase", "SpaceBlack", "SpaceBounce", "TitleText" })
                Assert.DoesNotThrow(() => RingPalette.Lin(t), "palette token " + t);
            var c = RingPalette.Lin("CeramicWhite");
            Assert.AreEqual(0.815f, c.r, 0.002f); Assert.AreEqual(0.791f, c.g, 0.002f); Assert.AreEqual(0.730f, c.b, 0.002f);
            var k = RingPalette.Lin("KeyLight");
            Assert.AreEqual(1.0f, k.r, 0.002f); Assert.AreEqual(0.761f, k.g, 0.002f); Assert.AreEqual(0.479f, k.b, 0.002f);
        }
    }
}
