#ifndef ABYSS_SURFACE_RECIPES_INCLUDED
#define ABYSS_SURFACE_RECIPES_INCLUDED

// =====================================================================================================================
// AbyssSurfaceRecipes.hlsl - material recipes of "Megastructure/Surface" (14.2 Materials and shader recipes).
// _Recipe: 0 ConcreteBoard, 1 ConcreteFloor, 2 ConcreteCyl, 3 SteelPainted, 4 SteelWeathered, 5 RodPolished,
//          6 Cable, 7 DoorLeaf, 8 HoneyTube.
// Every pattern is analytic and box-filtered with the pixel footprint (fades to its mean tone under 1.5 px); normal
// detail is the exact box filter of the height gradient over the footprint (finite difference of the height across the
// footprint), so grooves never sparkle. Every hash input is an integer cell index taken modulo its period count over the
// 1332 m wrap (rows 0.2 m -> 6660, boards 2.4 m -> 555, tie holes 1.2 x 1.0 m -> 1110 x 1332, lifts 3 m -> 444,
// expansion joints 12 m -> 111, macro noise 9 m -> 148 and 37 m -> 36).
// Requires the Surface CBUFFER (Surface.shader), AbyssCommon.hlsl and AbyssLighting.hlsl.
// =====================================================================================================================

#define AB_RECIPE_CONCRETE_BOARD 0
#define AB_RECIPE_CONCRETE_FLOOR 1
#define AB_RECIPE_CONCRETE_CYL   2
#define AB_RECIPE_STEEL_PAINTED  3
#define AB_RECIPE_STEEL_WEATHERED 4
#define AB_RECIPE_ROD_POLISHED   5
#define AB_RECIPE_CABLE          6
#define AB_RECIPE_DOOR_LEAF      7
#define AB_RECIPE_HONEY_TUBE     8

// Board-formed concrete dimensions (14.2 M_ConcreteBoard).
#define AB_BOARD_H        0.20
#define AB_BOARD_ROWS     6660      // 1332 / 0.2
#define AB_BOARD_L        2.4
#define AB_BOARD_COLS     555       // 1332 / 2.4
#define AB_GROOVE_W       0.003
#define AB_GROOVE_D       0.0015
#define AB_TIE_PU         1.2
#define AB_TIE_PU_COUNT   1110      // 1332 / 1.2
#define AB_TIE_PV         1.0
#define AB_TIE_R          0.0125    // 25 mm hole
#define AB_TIE_D          0.02
#define AB_LIFT_H         3.0
#define AB_LIFT_COUNT     444       // 1332 / 3
#define AB_COLD_W         0.008
#define AB_COLD_STEP      0.004
#define AB_EXP_P          12.0
#define AB_EXP_COUNT      111       // 1332 / 12
#define AB_EXP_W          0.025
#define AB_EXP_D          0.02
#define AB_MACRO_A        9.0
#define AB_MACRO_A_N      148       // 1332 / 9
#define AB_MACRO_B        37.0
#define AB_MACRO_B_N      36        // 1332 / 37
#define AB_STREAK_PU      12.0
#define AB_STREAK_PV      36.0

// Read-at-distance structure of board-formed concrete (R1-08 / R1-09: boards, lifts and joints must survive 5-500 m).
// R2-02: the board-scale terms were too weak where the scale cue depends on them (KS1 gallery walls at 1-2 m: board steps of
// about 1 level; KS3 C1 face below the collar at 17-30 levels: a +/-5 % step is 1-2 levels). Every board, lift and joint
// term is stronger near the eye, and the joint terms are drawn at least 1.5 px wide while they are resolved.
#define AB_BOARD_TONE     0.075     // per-board albedo +/-7.5 % (hashed by integer board index; R2-02 asks +/-6-8 %)
#define AB_BOARD_ROUGH    0.05      // per-board roughness +/-0.05
#define AB_BOARD_FADE0    2.5       // board-scale tone fades between 2.5 and 5 px rows: no 1.5-3 px hatching
#define AB_BOARD_FADE1    5.0
#define AB_BOARDLINE_DARK 0.30      // board joints (3 mm): darkening of the line drawn at least 1.5 px wide
#define AB_BOARDLINE_PX0  6.0       // the held line fades to its true coverage between 12 and 6 px rows (no hatching)
#define AB_BOARDLINE_PX1  12.0
#define AB_BLEED_W        0.014     // grout bleed along each board row joint: a tapered dark band (14 mm, core 5.6 mm) ...
#define AB_BLEED_DARK     0.24      // ... -24 % at its core (R2-02: KS1 board steps of 1.8 levels at 1-2 m against the 3 gate)
#define AB_GRAIN_PV       0.6       // wood-grain imprint: StreakTex.g across 0.6 m of height (1332 / 0.6 = 2220)
#define AB_GRAIN_PU       7.2       // and 7.2 m along the board (1332 / 7.2 = 185)
#define AB_GRAIN_AMP      0.08      // +/-4 %
#define AB_LIFT_TONE      0.055     // per-lift tone, all bays of a lift: +/-5.5 % (horizontal pour bands; R2-02 +/-6-8 % step)
#define AB_POUR_TONE      0.035     // per-pour tone (12 m bay x 3 m lift): +/-3.5 %
#define AB_BAY_TONE       0.045     // per pour campaign (12 m bay x 4 lifts): +/-4.5 % (R2-32: breaks the graph-paper tiles)
#define AB_RECESS_W       0.025     // rustication recess at each cold joint (25 mm, darkened; the 8 mm joint stays inside)
#define AB_RECESS_DARK    0.45
#define AB_LIP_W          0.015     // lower lip just under the recess: an upward-facing arris that catches the skylight
#define AB_LIP_LIGHT      0.18
#define AB_STAIN_L        0.10      // weathering band under each cold joint, exp(-t / 0.10): about 23 cm visible
#define AB_STAIN_DARK_NEAR 0.30     // -30 % at walking distances (R2-02: -25 to -30 %), -18 % on the far Face (graph paper)
#define AB_STAIN_DARK_FAR 0.18
#define AB_JOINT_VAR      0.40      // joint darkness varies +/-40 % per (bay, lift) segment (R2-32)
#define AB_JSTREAK_W      0.5       // rain streak along each 12 m expansion joint (drawn at least 1.5 px wide)
#define AB_JSTREAK_DARK   0.11
#define AB_JSTREAK_L      1.4       // strongest just under each cold joint, decaying down the lift
#define AB_STREAK_DARK    0.22      // StreakTex water streaks: +11 % darker / -4 % lighter, rougher in the runs (R2-02 +/-10 %)

// Worn concrete floor (R1-28).
#define AB_FLOOR_SAW_P    6.0       // saw cuts every 6 m (1332 / 6 = 222)
#define AB_FLOOR_SAW_W    0.006
#define AB_FLOOR_EXP_P    12.0      // expansion joints every 12 m (sealant, dirt band)
#define AB_FLOOR_EXP_W    0.018
#define AB_FLOOR_DIRT_W   0.06
#define AB_FLOOR_SLAB_N   222       // 6 m slabs per wrap (per-slab tone)
#define AB_FLOOR_AGG_A    0.75      // aggregate: NoiseTex.a at 0.75 m (2.9 mm texels) and .b at 0.9 m
#define AB_FLOOR_AGG_B    0.9
#define AB_DAMP_TILE      7.4       // damp patches: NoiseTex.g at 7.4 m (1332 / 7.4 = 180)

// Rust / stain patch lookups (two equalised lookups at incommensurate tiles, AbUniformMax2).
#define AB_PATCH_TILE_A   14.8      // 1332 / 14.8 = 90 (patches 0.46-3.7 m; 7.4 m left 0.2 m "leopard" spots, r01)
#define AB_PATCH_TILE_B   9.25      // 1332 / 9.25 = 144; common period 74 m

// Everything a recipe needs about the fragment.
struct AbRecipeInput
{
    float3 P;            // pattern position (world - 1332 k)
    float3 dPdx;         // screen derivatives of P (from the precise rotated local position)
    float3 dPdy;
    float3 N;            // geometric normal, unit, facing the viewer
    float3 positionWS;
    float2 uv0;
    float2 duvdx;
    float2 duvdy;
    float4 color;        // R AO, G edge wear, B generator flag
};

// Configuration of the board pattern in one projection.
struct AbBoardCfg
{
    float colLen;        // board length (wall) or panel width (cylinder)
    int   colCount;      // board columns per wrap (wall 555) or panels per circumference (cylinder N)
    float expPeriod;     // expansion joint spacing (wall 12 m; cylinder C / joints; <= 0 none)
    int   expCount;      // bays per wrap (wall 111; cylinder = joints; 1 when there are no joints)
    float tiePu;         // tie-hole pitch along u (wall 1.2 m; cylinder = panel width)
    int   tieCount;      // tie-hole columns per wrap
    float streakScaleU;  // StreakTex u per metre
    float cylinder;      // 1: cylinder mode (vertical formwork panel joints at every panel boundary, no stagger)
    float streaks;       // 1: vertical water streaks (walls), 0: horizontal faces
};

struct AbBoardOut
{
    float  albedoMul;    // multiplicative tone (1 = base)
    float  roughAdd;     // roughness offset (boards, water streaks)
    float2 slope;        // filtered (dh/du, dh/dv)
};

AbBoardCfg AbWallBoardCfg()
{
    AbBoardCfg c;
    c.colLen = AB_BOARD_L;
    c.colCount = AB_BOARD_COLS;
    c.expPeriod = AB_EXP_P;
    c.expCount = AB_EXP_COUNT;
    c.tiePu = AB_TIE_PU;
    c.tieCount = AB_TIE_PU_COUNT;
    c.streakScaleU = 1.0 / AB_STREAK_PU;
    c.cylinder = 0.0;
    c.streaks = 1.0;
    return c;
}

// Butt-joint offset of a board row (staggered by hash), metres in [0, colLen).
float AbRowOffset(int row, AbBoardCfg cfg)
{
    return cfg.colLen * AbHash1(row, AB_BOARD_ROWS, 11u);
}

// Board-formed concrete height field (metres, along the surface normal) at (u, v). k* are the detail fade weights.
// kGrooveH fades the horizontal board grooves (a v feature, by the v footprint), kGrooveB the butt / panel joints
// (a u feature, by the u footprint).
float AbBoardHeight(float u, float v, AbBoardCfg cfg, float kGrooveH, float kGrooveB, float kCold, float kExp, float kTie)
{
    float h = AbGrooveH(v, AB_BOARD_H, AB_GROOVE_W, AB_GROOVE_D * kGrooveH);
    int row = AbCell(v, AB_BOARD_H);
    if (cfg.cylinder > 0.5)
    {
        h += AbGrooveH(u, cfg.colLen, AB_GROOVE_W, AB_GROOVE_D * kGrooveB);  // full-height formwork panel joints
    }
    else
    {
        h += AbGrooveH(u + AbRowOffset(row, cfg), cfg.colLen, AB_GROOVE_W, AB_GROOVE_D * kGrooveB);
    }
    // cold joint: 8 mm band, and the lift above stands 4 mm proud (sawtooth: step at each joint, flat in between)
    h += AbGrooveH(v, AB_LIFT_H, AB_COLD_W, 0.001 * kCold);
    h += AB_COLD_STEP * kCold * (1.0 - frac(v / AB_LIFT_H));
    // expansion joint: 25 mm gap
    if (cfg.expPeriod > 0.0) h += AbGrooveH(u, cfg.expPeriod, AB_EXP_W, AB_EXP_D * kExp);
    // tie holes: 25 mm cones on the 1.2 x 1.0 m grid (centred in a board row)
    float tu = u - cfg.tiePu * (floor(u / cfg.tiePu) + 0.5);
    float tv = v - AB_TIE_PV * (floor(v / AB_TIE_PV) + 0.5);
    h -= AB_TIE_D * kTie * saturate(1.0 - sqrt(tu * tu + tv * tv) / AB_TIE_R);
    return h;
}

// Tone of one board (column col of row) in [-1, 1].
float AbBoardJitter(int col, int row, AbBoardCfg cfg)
{
    return AbHash2(int2(col, row), int2(cfg.colCount, AB_BOARD_ROWS), 31u) * 2.0 - 1.0;
}

// Box-filtered per-board tone of a row at u (blends across butt joints within the footprint).
float AbRowTone(float u, int row, float fwU, AbBoardCfg cfg)
{
    float x = (cfg.cylinder > 0.5 ? u : u + AbRowOffset(row, cfg)) / cfg.colLen;
    int col = (int)floor(x);
    float2 w = AbCellBlend(x, fwU / cfg.colLen);
    float t = AbBoardJitter(col, row, cfg) * (1.0 - w.x - w.y);
    t += AbBoardJitter(col - 1, row, cfg) * w.x;
    t += AbBoardJitter(col + 1, row, cfg) * w.y;
    return t;
}

// Board joint line (3 mm) of period p: drawn at least 1.5 px wide with contrast x sqrt(true / drawn) while k = 1, the true
// box-filtered coverage (fading to its mean under 1.5 px) while k = 0. k fades with the row spacing (12 -> 6 px).
float AbBoardLine(float x, float p, float fw, float k)
{
    float drawn = max(AB_GROOVE_W, 1.5 * fw);
    float held = AbLineBox(x, p, drawn, fw) * sqrt(AB_GROOVE_W / drawn);
    return lerp(AbLineCoverage(x, p, AB_GROOVE_W, fw), held, k);
}

// Narrow joint band of true width w on period p (recess, lip): the true coverage when the band is under 0.35 px, drawn at
// least 1.5 px wide with contrast x sqrt(w / drawn) once it is over 0.7 px (the near joints read as a light / dark pair).
float AbJointBand(float x, float p, float w, float fw)
{
    float kNear = smoothstep(0.35, 0.7, w / max(fw, 1e-6));
    float drawn = max(w, 1.5 * fw);
    return lerp(AbLineCoverage(x, p, w, fw), AbLineBox(x, p, drawn, fw) * sqrt(w / drawn), kNear);
}

// Board-formed concrete in one planar projection: u along the surface (horizontal for walls), v up (walls) / second
// horizontal axis (floors and soffits). fwU / fwV are the pixel footprints of u and v in metres.
AbBoardOut AbConcreteBoard(float u, float v, float fwU, float fwV, AbBoardCfg cfg, float2 streakUV, float2 dStreakDx, float2 dStreakDy)
{
    AbBoardOut o;
    float fwAvg = 0.5 * (fwU + fwV);
    float kGrooveV = AbFeatureFade(AB_GROOVE_W / max(fwV, 1e-6));
    float kGrooveU = AbFeatureFade(AB_GROOVE_W / max(fwU, 1e-6));
    float kCold = AbFeatureFade(AB_COLD_W / max(fwV, 1e-6));
    float kExp = AbFeatureFade(AB_EXP_W / max(fwU, 1e-6));
    float kTie = AbFeatureFade(2.0 * AB_TIE_R / max(fwAvg, 1e-6));

    // ---- normal detail: exact box filter of the height gradient over the pixel footprint
    float au = max(0.5 * fwU, 1e-5);
    float av = max(0.5 * fwV, 1e-5);
    float hu1 = AbBoardHeight(u + au, v, cfg, kGrooveV, kGrooveU, kCold, kExp, kTie);
    float hu0 = AbBoardHeight(u - au, v, cfg, kGrooveV, kGrooveU, kCold, kExp, kTie);
    float hv1 = AbBoardHeight(u, v + av, cfg, kGrooveV, kGrooveU, kCold, kExp, kTie);
    float hv0 = AbBoardHeight(u, v - av, cfg, kGrooveV, kGrooveU, kCold, kExp, kTie);
    o.slope = float2((hu1 - hu0) / (2.0 * au), (hv1 - hv0) / (2.0 * av));

    // ---- tone: board joints, cold joints, expansion joints, tie holes (filtered coverage, mean under 1.5 px). R2-02: the
    //      3 mm board joints are held at 1.5 px while the 0.2 m rows are over 12 px apart (gallery walls, C1 at the ledge),
    //      so board-formed concrete reads as boards at walking distance; from 12 to 6 px rows they fade to the true coverage
    int row = AbCell(v, AB_BOARD_H);
    float kRows = smoothstep(AB_BOARDLINE_PX0, AB_BOARDLINE_PX1, AB_BOARD_H / max(fwV, 1e-6));
    float kCols = smoothstep(AB_BOARDLINE_PX0, AB_BOARDLINE_PX1, cfg.colLen / max(fwU, 1e-6));
    float cGrooveH = AbBoardLine(v, AB_BOARD_H, fwV, kRows);
    float cGrooveB;
    if (cfg.cylinder > 0.5)
        cGrooveB = AbBoardLine(u, cfg.colLen, fwU, kCols);                  // panel joints (every panel boundary)
    else
        cGrooveB = AbBoardLine(u + AbRowOffset(row, cfg), cfg.colLen, fwU, kCols * kRows);   // staggered butt joints
    float cGroove = 1.0 - (1.0 - cGrooveH) * (1.0 - cGrooveB);
    // grout bleed (R2-02): paste lost through each row joint darkens a 1-1.5 cm band along it, so the 0.2 m board rows
    // read at walking distance under flat light (two nested boxes of 14 and 5.6 mm: a tapered band); it fades to its mean
    // with the held board line (rows 12 -> 6 px), so it never hatches
    float cBleed = 0.5 * (AbLineBox(v, AB_BOARD_H, AB_BLEED_W, fwV) + AbLineBox(v, AB_BOARD_H, 0.4 * AB_BLEED_W, fwV));
    cBleed = lerp(0.7 * AB_BLEED_W / AB_BOARD_H, cBleed, kRows);
    float cCold = AbLineCoverage(v, AB_LIFT_H, AB_COLD_W, fwV);
    float cExp = cfg.expPeriod > 0.0 ? AbLineCoverage(u, cfg.expPeriod, AB_EXP_W, fwU) : 0.0;
    float tu = u - cfg.tiePu * (floor(u / cfg.tiePu) + 0.5);
    float tv = v - AB_TIE_PV * (floor(v / AB_TIE_PV) + 0.5);
    float tieMean = PI * AB_TIE_R * AB_TIE_R / (cfg.tiePu * AB_TIE_PV);
    float cTie = AbDiscCoverage(sqrt(tu * tu + tv * tv), AB_TIE_R, fwAvg, tieMean);
    float tone = (1.0 - AB_BOARDLINE_DARK * cGroove) * (1.0 - AB_BLEED_DARK * cBleed) * (1.0 - 0.22 * cCold) * (1.0 - 0.70 * cExp) * (1.0 - 0.60 * cTie);
    float rough = 0.0;

    // ---- per-board tone (+/-5.5 %) and roughness (+/-0.05), box-filtered across row and butt-joint boundaries; the
    //      board scale fades out between 2.5 and 5 px rows (a random tone per 1.5-3 px row reads as hatching)
    float rowX = v / AB_BOARD_H;
    float2 wr = AbCellBlend(rowX, fwV / AB_BOARD_H);
    float kBoard = AbFeatureFadeRange(AB_BOARD_H / max(fwV, 1e-6), AB_BOARD_FADE0, AB_BOARD_FADE1);
    float jitter = 0.0;
    if (kBoard > 0.0)
    {
        jitter = AbRowTone(u, row, fwU, cfg) * (1.0 - wr.x - wr.y);
        if (wr.x > 0.0) jitter += AbRowTone(u, row - 1, fwU, cfg) * wr.x;
        if (wr.y > 0.0) jitter += AbRowTone(u, row + 1, fwU, cfg) * wr.y;
        jitter *= kBoard;

        // wood-grain imprint along each board (StreakTex.g lines run along u), offset per board, mip-filtered
        float xb = (cfg.cylinder > 0.5 ? u : u + AbRowOffset(row, cfg)) / cfg.colLen;
        float gOff = AbHash2(int2((int)floor(xb), row), int2(cfg.colCount, AB_BOARD_ROWS), 67u);
        float2 guv = float2(v / AB_GRAIN_PV + 0.37 * gOff, u / AB_GRAIN_PU + gOff);
        float2 gdx = float2(fwV / AB_GRAIN_PV, fwU / AB_GRAIN_PU);
        float grain = SAMPLE_TEXTURE2D_GRAD(_AbStreakTex, sampler_AbStreakTex, guv, float2(gdx.x, 0.0), float2(0.0, gdx.y)).g;
        tone *= 1.0 + AB_GRAIN_AMP * (grain - 0.5) * kBoard;
    }
    tone *= 1.0 + AB_BOARD_TONE * jitter;
    rough += AB_BOARD_ROUGH * jitter;

    // ---- pour lifts (3 m) and bays (12 m): per-lift tone +/-5.5 %, per-pour (bay x lift) tone +/-3.5 % and per pour
    //      campaign (bay x 4 lifts) +/-4.5 %, box-filtered across lift and bay boundaries; they fade only when a lift is
    //      under 1.5-3 px (300 m+ on a 66-degree 1080p frame). R2-02: each lift steps by about +/-6-8 %; R2-32: the
    //      campaign tone breaks the regular graph-paper tiles of the Face
    float liftX = v / AB_LIFT_H;
    int lift = (int)floor(liftX);
    float2 wl = AbCellBlend(liftX, fwV / AB_LIFT_H);
    float bayW = cfg.expPeriod > 0.0 ? cfg.expPeriod : 1332.0;
    float bayX = u / bayW;
    int bay = (int)floor(bayX);
    float2 wb = AbCellBlend(bayX, fwU / bayW);
    int2 pourN = int2(max(cfg.expCount, 1), AB_LIFT_COUNT);
    int2 campN = int2(max(cfg.expCount, 1), AB_LIFT_COUNT / 4);
    float lt = 0.0;
    [unroll] for (int dl = -1; dl <= 1; dl++)
    {
        float wv = dl == 0 ? 1.0 - wl.x - wl.y : (dl < 0 ? wl.x : wl.y);
        if (wv <= 0.0) continue;
        int L = lift + dl;
        int C = L >> 2;                                                        // arithmetic shift: floor(L / 4)
        float pour = (AbHash2(int2(bay, L), pourN, 53u) * 2.0 - 1.0) * (1.0 - wb.x - wb.y)
                   + (AbHash2(int2(bay - 1, L), pourN, 53u) * 2.0 - 1.0) * wb.x
                   + (AbHash2(int2(bay + 1, L), pourN, 53u) * 2.0 - 1.0) * wb.y;
        float camp = (AbHash2(int2(bay, C), campN, 59u) * 2.0 - 1.0) * (1.0 - wb.x - wb.y)
                   + (AbHash2(int2(bay - 1, C), campN, 59u) * 2.0 - 1.0) * wb.x
                   + (AbHash2(int2(bay + 1, C), campN, 59u) * 2.0 - 1.0) * wb.y;
        float liftTone = AbHash1(L, AB_LIFT_COUNT, 41u) * 2.0 - 1.0;
        lt += wv * (AB_LIFT_TONE * liftTone + AB_POUR_TONE * pour + AB_BAY_TONE * camp);
    }
    tone *= 1.0 + lt * AbFeatureFade(AB_LIFT_H / max(fwV, 1e-6));

    // ---- cold joints (R2-02): a 25 mm rustication recess (dark) with an upward-facing lower lip that catches the skylight
    //      (light), both drawn at least 1.5 px wide while resolved (within about 35 m), and a weathering band under each
    //      joint (exp(-t / 10 cm), -30 % near, -18 % on the far Face), drawn at least 0.6 px deep so it survives as a soft
    //      line out to 500 m. The darkness of every joint segment (bay, lift) varies by +/-40 % (R2-32: no graph paper)
    float jv = 1.0 + AB_JOINT_VAR * (2.0 * AbHash2(int2(bay, lift), pourN, 89u) - 1.0);
    float cRecess = AbJointBand(v, AB_LIFT_H, AB_RECESS_W, fwV);
    float kNearJ = smoothstep(0.35, 0.7, AB_RECESS_W / max(fwV, 1e-6));
    float drawnR = lerp(AB_RECESS_W, max(AB_RECESS_W, 1.5 * fwV), kNearJ);
    float drawnL = lerp(AB_LIP_W, max(AB_LIP_W, 1.5 * fwV), kNearJ);
    float cLip = AbJointBand(v + 0.5 * (drawnR + drawnL), AB_LIFT_H, AB_LIP_W, fwV);   // just under the (drawn) recess
    float below = AB_LIFT_H * (1.0 - frac(v / AB_LIFT_H));                    // distance below the joint above (0..3)
    float stainL = max(AB_STAIN_L, 0.6 * fwV);
    float stain = AbExpStainBox(below, stainL, AB_LIFT_H, 0.5 * fwV) * sqrt(AB_STAIN_L / stainL) * smoothstep(4.0, 8.0, AB_LIFT_H / max(fwV, 1e-6));
    float stainDark = lerp(AB_STAIN_DARK_NEAR, AB_STAIN_DARK_FAR, smoothstep(0.01, 0.05, fwV));
    tone *= (1.0 - saturate(AB_RECESS_DARK * jv) * cRecess) * (1.0 + AB_LIP_LIGHT * cLip) * (1.0 - saturate(stainDark * jv) * stain);
    rough += 0.05 * stain;

    // ---- rain streaks keyed to the 12 m expansion joints: a 0.5 m dark run under every joint crossing, strongest just
    //      under each cold joint, strength hashed per (joint, lift); drawn at least 1.5 px wide (vertical graph-paper lines)
    if (cfg.streaks > 0.5 && cfg.expPeriod > 0.0)
    {
        // soft-edged run: three nested boxes (40 / 70 / 100 % of the drawn width) average to a tapered profile
        float kJ, kJ2, kJ3;
        float cJ = (AbLineMinWidth(u, cfg.expPeriod, AB_JSTREAK_W, fwU, 1.5, kJ)
                  + AbLineMinWidth(u, cfg.expPeriod, 0.7 * AB_JSTREAK_W, fwU, 1.05, kJ2)
                  + AbLineMinWidth(u, cfg.expPeriod, 0.4 * AB_JSTREAK_W, fwU, 0.6, kJ3)) * (1.0 / 3.0);
        int jc = (int)round(u / cfg.expPeriod);
        float js = 0.55 + 0.45 * AbHash2(int2(jc, lift), pourN, 71u);
        float decay = 0.45 + 0.55 * exp(-below / AB_JSTREAK_L);
        tone *= 1.0 - AB_JSTREAK_DARK * kJ * cJ * js * decay;
        rough += 0.06 * cJ * kJ;
    }

    // ---- vertical water streaks (StreakTex 12 m x 36 m), darker and rougher runs, concentrated below each cold joint
    if (cfg.streaks > 0.5)
    {
        float s = SAMPLE_TEXTURE2D_GRAD(_AbStreakTex, sampler_AbStreakTex, streakUV, dStreakDx, dStreakDy).r;
        float w = 0.35 + 0.65 * smoothstep(1.6, 0.0, below) * smoothstep(0.0, 0.05, below);
        float d = s - 0.5;
        tone *= 1.0 - AB_STREAK_DARK * (d > 0.0 ? d : 0.4 * d) * w;
        rough += 0.10 * saturate(2.0 * d) * w;
    }

    o.albedoMul = tone;
    o.roughAdd = rough;
    return o;
}

// Macro variation of concrete: value noise with lattice periods 9 m and 37 m, +/-12 % albedo (14.2).
float AbConcreteMacro(float3 P)
{
    float n = 0.6 * AbValueNoise3(P, AB_MACRO_A, AB_MACRO_A_N, 101u) + 0.4 * AbValueNoise3(P, AB_MACRO_B, AB_MACRO_B_N, 211u);
    return 1.0 + 0.24 * (n - 0.5) * _MacroAmp;
}

// Triplanar sample of the noise texture (tile in metres, a divisor of 1332) with analytic gradients.
float4 AbNoiseTriplanar(AbRecipeInput ri, float tile)
{
    float3 w = abs(ri.N);
    w = w * w * w;
    w /= max(w.x + w.y + w.z, 1e-5);
    float s = 1.0 / tile;
    float4 nx = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, ri.P.zy * s, ri.dPdx.zy * s, ri.dPdy.zy * s);
    float4 ny = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, ri.P.xz * s, ri.dPdx.xz * s, ri.dPdy.xz * s);
    float4 nz = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, ri.P.xy * s, ri.dPdx.xy * s, ri.dPdy.xy * s);
    return nx * w.x + ny * w.y + nz * w.z;
}

// Wall frame of a (mostly vertical) surface: horizontal tangent from the normal.
float2 AbWallTangent(float3 N)
{
    float2 t = float2(N.z, -N.x);
    float l = length(t);
    return l > 1e-4 ? t / l : float2(1.0, 0.0);
}

// Board-formed concrete on any orientation: wall projection (u along the wall, v = height) for vertical faces, floor
// projection (x, z) for horizontal faces, blended over |N.y| 0.6-0.8. Returns albedo multiplier and perturbed normal.
void AbConcreteBoardAny(AbRecipeInput ri, AbBoardCfg cfg, out float albedoMul, out float roughAdd, out float3 normalWS)
{
    float ay = abs(ri.N.y);
    float wFloor = smoothstep(0.6, 0.8, ay);
    float mul = 0.0;
    float rough = 0.0;
    float3 dn = float3(0.0, 0.0, 0.0);
    if (wFloor < 1.0)
    {
        float2 t = AbWallTangent(ri.N);
        float3 Tu = float3(t.x, 0.0, t.y);
        float u = dot(ri.P.xz, t);
        float v = ri.P.y;
        float fwU = AbFootprint(ri.dPdx, ri.dPdy, Tu);
        float fwV = AbFootprint(ri.dPdx, ri.dPdy, float3(0.0, 1.0, 0.0));
        float2 sUV = float2(u * cfg.streakScaleU, v / AB_STREAK_PV);
        float2 sdx = float2(dot(ri.dPdx, Tu) * cfg.streakScaleU, ri.dPdx.y / AB_STREAK_PV);
        float2 sdy = float2(dot(ri.dPdy, Tu) * cfg.streakScaleU, ri.dPdy.y / AB_STREAK_PV);
        AbBoardOut b = AbConcreteBoard(u, v, fwU, fwV, cfg, sUV, sdx, sdy);
        float k = 1.0 - wFloor;
        mul += b.albedoMul * k;
        rough += b.roughAdd * k;
        dn += (b.slope.x * Tu + b.slope.y * float3(0.0, 1.0, 0.0)) * k;
    }
    if (wFloor > 0.0)
    {
        AbBoardCfg fc = cfg;
        fc.streaks = 0.0;
        float fwU = AbFootprint(ri.dPdx, ri.dPdy, float3(1.0, 0.0, 0.0));
        float fwV = AbFootprint(ri.dPdx, ri.dPdy, float3(0.0, 0.0, 1.0));
        AbBoardOut b = AbConcreteBoard(ri.P.x, ri.P.z, fwU, fwV, fc, float2(0.0, 0.0), float2(0.0, 0.0), float2(0.0, 0.0));
        mul += b.albedoMul * wFloor;
        rough += b.roughAdd * wFloor;
        dn += (b.slope.x * float3(1.0, 0.0, 0.0) + b.slope.y * float3(0.0, 0.0, 1.0)) * wFloor;
    }
    albedoMul = mul;
    roughAdd = rough;
    normalWS = AbSafeNormalize(ri.N - dn, ri.N);
}

// Sub-pixel bevel resolve of a recipe (R1-26): returns k (1 = the chamfer / worn edge is resolved, 0 = sub-pixel) and
// bends a sub-pixel 45-degree chamfer's normal onto its camera-facing neighbour face (AbBevelResolve).
float AbRecipeBevel(AbRecipeInput ri, inout float3 normalWS)
{
    float3 faceN;
    float k = AbBevelResolve(ri.N, ri.positionWS, saturate(ri.color.g), _BevelWidth, faceN);
    normalWS = dot(faceN, ri.N) < 0.9999 ? faceN : normalWS;   // bent chamfer: the face normal; else keep the detail
    return k;
}

// Shared concrete finishing: macro variation, fine mottling, AO and edge wear (faded with the bevel's projected width).
void AbConcreteFinish(AbRecipeInput ri, inout AbSurface s, float albedoMul, float roughAdd)
{
    float4 nz = AbNoiseTriplanar(ri, 1.5);
    float mottle = 1.0 + 0.06 * (nz.b - 0.5);
    float3 n = s.normalWS;
    float kBevel = AbRecipeBevel(ri, n);
    s.normalWS = n;
    float wear = saturate(ri.color.g) * _WearAmount * kBevel;
    s.albedo = _BaseColor.rgb * albedoMul * AbConcreteMacro(ri.P) * mottle * (1.0 + 0.25 * wear);
    s.roughness = saturate(_Roughness + roughAdd + 0.06 * wear + 0.04 * (nz.r - 0.5));
    s.metallic = 0.0;
    s.ao = saturate(ri.color.r);
}

// ------------------------------------------------------------------------------------------------ recipes
AbSurface AbRecipeConcreteBoard(AbRecipeInput ri)
{
    AbSurface s = AbDefaultSurface(ri.N);
    float mul, rough;
    float3 n;
    AbConcreteBoardAny(ri, AbWallBoardCfg(), mul, rough, n);
    s.normalWS = n;
    AbConcreteFinish(ri, s, mul, rough);
    return s;
}

// Worn concrete floor: trowelled slab, 5 mm saw cuts every 3 m, mottling, worn centre path from UV0.x
// (signed lateral offset from the walking centreline in metres; _WearWidth <= 0 disables the path).
// Slab joint along one axis: saw cuts (6 m), expansion joints (12 m, sealant) and a dirt band beside both; returns the
// albedo factor and adds the filtered groove slope.
float AbFloorJoints(float x, float fw, out float slope)
{
    float cSaw = AbLineCoverage(x, AB_FLOOR_SAW_P, AB_FLOOR_SAW_W, fw);
    float cExp = AbLineCoverage(x, AB_FLOOR_EXP_P, AB_FLOOR_EXP_W, fw);
    // the dirt band is held at >= 1.5 px (contrast x sqrt(true / drawn)) so the 6 m slab rhythm survives at grazing angles
    float kDirt;
    float cDirt = AbLineMinWidth(x, AB_FLOOR_SAW_P, AB_FLOOR_DIRT_W, fw, 1.5, kDirt) * kDirt * 1.6;
    float kS = AbFeatureFade(AB_FLOOR_SAW_W / max(fw, 1e-6));
    float kE = AbFeatureFade(AB_FLOOR_EXP_W / max(fw, 1e-6));
    float a = max(0.5 * fw, 1e-5);
    float h1 = AbGrooveH(x + a, AB_FLOOR_SAW_P, AB_FLOOR_SAW_W, 0.004 * kS) + AbGrooveH(x + a, AB_FLOOR_EXP_P, AB_FLOOR_EXP_W, 0.006 * kE);
    float h0 = AbGrooveH(x - a, AB_FLOOR_SAW_P, AB_FLOOR_SAW_W, 0.004 * kS) + AbGrooveH(x - a, AB_FLOOR_EXP_P, AB_FLOOR_EXP_W, 0.006 * kE);
    slope = (h1 - h0) / (2.0 * a);
    return (1.0 - 0.5 * cSaw) * (1.0 - 0.65 * cExp) * (1.0 - 0.10 * cDirt);
}

// Box-filtered per-slab tone (6 m slabs) along one axis for a fixed slab index on the other axis.
float AbFloorSlabRow(float x, int other, float fw, bool alongX)
{
    float c = x / AB_FLOOR_SAW_P;
    int i = (int)floor(c);
    float2 w = AbCellBlend(c, fw / AB_FLOOR_SAW_P);
    int2 n = int2(AB_FLOOR_SLAB_N, AB_FLOOR_SLAB_N);
    float t0 = AbHash2(alongX ? int2(i, other) : int2(other, i), n, 83u);
    float tm = AbHash2(alongX ? int2(i - 1, other) : int2(other, i - 1), n, 83u);
    float tp = AbHash2(alongX ? int2(i + 1, other) : int2(other, i + 1), n, 83u);
    return (t0 * (1.0 - w.x - w.y) + tm * w.x + tp * w.y) * 2.0 - 1.0;
}

// Worn concrete floor (14.2 roughness 0.45, worn centre path; R1-28): trowelled 6 m slabs with saw cuts, 12 m expansion
// joints with sealant and a dirt band, per-slab tone +/-4 %, aggregate micro-variation +/-3 % (NoiseTex mips fade it),
// mottling, macro, darker damp patches (biased toward worn edges), kerb edge wear from vertex G (faded by its projected
// width), and the worn centre path from UV0.x (signed lateral offset from the walking centreline in metres; _WearWidth
// <= 0 disables it): a lighter, smoother band with exposed aggregate.
AbSurface AbRecipeConcreteFloor(AbRecipeInput ri)
{
    AbSurface s = AbDefaultSurface(ri.N);
    float ay = abs(ri.N.y);
    float wTop = smoothstep(0.6, 0.9, ay);
    float mul = 1.0;
    float rough = 0.0;
    float3 dn = float3(0.0, 0.0, 0.0);
    if (ay > 0.6)
    {
        float fwX = AbFootprint(ri.dPdx, ri.dPdy, float3(1.0, 0.0, 0.0));
        float fwZ = AbFootprint(ri.dPdx, ri.dPdy, float3(0.0, 0.0, 1.0));
        float sx, sz;
        mul *= AbFloorJoints(ri.P.x, fwX, sx) * AbFloorJoints(ri.P.z, fwZ, sz);
        dn = float3(sx, 0.0, sz);
        // per-slab tone: blend along x for the current z slab (z blending is below the eye's resolution for 6 m slabs)
        int slabZ = (int)floor(ri.P.z / AB_FLOOR_SAW_P);
        float slab = AbFloorSlabRow(ri.P.x, slabZ, fwX, true);
        float kSlab = AbFeatureFade(AB_FLOOR_SAW_P / max(max(fwX, fwZ), 1e-6) / 2.0);
        mul *= 1.0 + 0.06 * slab * kSlab * wTop;          // R2-14: +/-6 % (was 4 %: under 1 level on the dark landing)
        rough += 0.04 * slab * kSlab * wTop;
    }
    s.normalWS = AbSafeNormalize(ri.N - dn, ri.N);

    float4 nm = AbNoiseTriplanar(ri, 3.7);                 // mottling (+/-8 %), 3.7 m tile (1332 / 3.7 = 360)
    float4 na = AbNoiseTriplanar(ri, AB_FLOOR_AGG_A);       // aggregate, 2.9 mm texels
    float4 nb = AbNoiseTriplanar(ri, AB_FLOOR_AGG_B);
    float4 nd = AbNoiseTriplanar(ri, AB_DAMP_TILE);         // damp patches (equalised channel g)
    float4 nt = AbNoiseTriplanar(ri, 1.5);                  // trowel / patch-repair variation, 1.5 m tile (R2-14)
    float wearPath = _WearWidth > 0.0 ? exp(-(ri.uv0.x * ri.uv0.x) / (_WearWidth * _WearWidth)) : 0.0;
    wearPath *= wTop;

    // R2-14: walking surfaces at 1-6 m read as flat graybox (high-pass 1.0-1.1 levels against 2.5 on the ledge floor):
    // stronger aggregate (+/-4 %), mottling (+/-8 %) and a mid-scale trowel term (+/-5 %, 1.5 m) that survives at 5-60 m
    // lead note (ledge_r32): KS2a deck 1.29 / B2_start landing 1.14 levels of high-pass against 2: aggregate +/-7 % + 3.5 %
    float aggregate = (na.a - 0.5) * 0.14 + (nb.b - 0.5) * 0.07;          // about +/-7 % (std about 4.5 %)
    aggregate *= 1.0 + 0.8 * wearPath;                                      // exposed on the worn path
    float macro = lerp(AbConcreteMacro(ri.P), 1.0, 0.4 * wearPath);
    float mottle = (1.0 + 0.16 * (nm.g - 0.5) * (1.0 - 0.4 * wearPath)) * (1.0 + 0.10 * (nt.r - 0.5) * wTop);

    // kerb / edge wear (vertex G), chipped by noise, faded with its projected width
    float3 nBev = s.normalWS;
    float kBevel = AbRecipeBevel(ri, nBev);
    s.normalWS = nBev;
    float edge = saturate(ri.color.g) * _WearAmount * kBevel * (0.5 + 0.5 * smoothstep(0.35, 0.65, nb.a));

    // damp patches: darker, slightly rougher (R1-28), more likely along worn edges and kerbs
    float damp = smoothstep(0.66, 0.92, nd.g + 0.12 * saturate(ri.color.g)) * (0.5 + 0.5 * nm.r) * wTop * 0.7;

    s.albedo = _BaseColor.rgb * mul * macro * mottle * (1.0 + aggregate) * (1.0 + 0.08 * wearPath)
             * (1.0 + 0.18 * edge) * (1.0 - 0.16 * damp);
    s.roughness = saturate(_Roughness + rough - 0.14 * wearPath + 0.07 * damp + 0.08 * (nm.r - 0.5) + 0.06 * (nt.a - 0.5) + 0.04 * edge);
    s.metallic = 0.0;
    s.ao = saturate(ri.color.r);
    return s;
}

// Board-formed cylinder (columns): UV0.x = arc length (m), v = pattern height; N = round(2 pi R / 1.2) panels
// (_PanelCount, _CylRadius) with a full-height joint at every panel boundary, _CylJoints vertical expansion joints,
// horizontal boards and lifts every 3 m as on walls. The u tangent is oriented by the screen derivatives of UV0.x, so
// joints read as grooves on outward columns and inward bores alike.
AbSurface AbRecipeConcreteCyl(AbRecipeInput ri)
{
    AbSurface s = AbDefaultSurface(ri.N);
    float circumference = 2.0 * PI * max(_CylRadius, 0.01);
    float panels = max(round(_PanelCount), 1.0);
    AbBoardCfg cfg;
    cfg.colLen = circumference / panels;
    cfg.colCount = (int)panels;
    cfg.expPeriod = _CylJoints > 0.5 ? circumference / round(_CylJoints) : 0.0;
    cfg.expCount = _CylJoints > 0.5 ? (int)round(_CylJoints) : 1;
    cfg.tiePu = cfg.colLen;
    cfg.tieCount = (int)panels;
    cfg.streakScaleU = max(round(circumference / AB_STREAK_PU), 1.0) / circumference;   // integer repeats per turn
    cfg.cylinder = 1.0;
    cfg.streaks = 1.0;

    float ay = abs(ri.N.y);
    float mul, rough;
    float3 n;
    if (ay < 0.6)
    {
        float u = ri.uv0.x;
        float v = ri.P.y;
        float2 t = AbWallTangent(ri.N);
        float3 Tu = float3(t.x, 0.0, t.y);
        // Tu must point toward increasing UV0.x for the height gradient to bend the normal the right way. MeshKit.Lathe
        // measures phi from +X toward +Z, so dP/du = (-sin phi, 0, cos phi): that is -AbWallTangent(N) on outward
        // columns and +AbWallTangent(N) on inward bores (and either under a mirrored transform). Take the orientation
        // from the screen derivatives: sum_i dot(dP_i, Tu) du_i = (du/ds) * sum_i dot(dP_i, Tu)^2 has the sign of du/ds
        // and never vanishes on a non-degenerate pixel (dPdx, dPdy span the tangent plane, Tu lies in it).
        float uOrient = dot(ri.dPdx, Tu) * ri.duvdx.x + dot(ri.dPdy, Tu) * ri.duvdy.x;
        Tu *= uOrient >= 0.0 ? 1.0 : -1.0;
        float fwU = abs(ri.duvdx.x) + abs(ri.duvdy.x);
        float fwV = AbFootprint(ri.dPdx, ri.dPdy, float3(0.0, 1.0, 0.0));
        float2 sUV = float2(u * cfg.streakScaleU, v / AB_STREAK_PV);
        float2 sdx = float2(ri.duvdx.x * cfg.streakScaleU, ri.dPdx.y / AB_STREAK_PV);
        float2 sdy = float2(ri.duvdy.x * cfg.streakScaleU, ri.dPdy.y / AB_STREAK_PV);
        AbBoardOut b = AbConcreteBoard(u, v, fwU, fwV, cfg, sUV, sdx, sdy);
        mul = b.albedoMul;
        rough = b.roughAdd;
        n = AbSafeNormalize(ri.N - (b.slope.x * Tu + b.slope.y * float3(0.0, 1.0, 0.0)), ri.N);
    }
    else
    {
        AbConcreteBoardAny(ri, AbWallBoardCfg(), mul, rough, n);   // band tops, collar tops: planar boards
    }
    s.normalWS = n;
    AbConcreteFinish(ri, s, mul, rough);
    return s;
}

// Painted steel (STEEL_DARK): roughness _Roughness (floor 0.25), edge wear from vertex G (bare steel), RUST by the
// coverage-exact equalised noise mask of AbRustMask (exactly _RustAmount of the surface, capped at 12 %), tinted subtly
// by AbRustColor (desaturated, about the paint's own luma).
// R1-27 / R1-33: no 3 m texture repetition (rust and stains come from two equalised lookups at 7.4 m and 4.5 m combined by
// AbUniformMax2, common period 333 m), rust biased onto worn edges (vertex G), a soft rust halo around patches, RUST hue on
// structure but dark-neutral stains on walking surfaces (N.y > 0.7), no fine "granite" speckle (paint +/-2 % on a 3 m
// tile), grime as low-frequency vertical value noise instead of 3 m mottling, edge wear faded by its projected width.
// Rust patch value of structural steel (R2-10: the crank wheel showed round, evenly scattered "leopard-print" decals). Two
// equalised lookups (14.8 / 9.25 m) combined by AbUniformMax2 (uniform: the AbRustMask threshold stays coverage-exact), each
// domain-warped by a 3.7 m lookup (+/-0.45 m: no round discs) and stretched 3:1 vertically on walls (runs that bleed
// downward from bolts and edges). Periods 14.8 / 44.4, 9.25 / 27.75 and 3.7 m all divide 1332.
float AbRustPatchG(AbRecipeInput ri, float tile, float2 offs)
{
    float3 w = abs(ri.N);
    w = w * w * w;
    w /= max(w.x + w.y + w.z, 1e-5);
    float4 wn = AbNoiseTriplanar(ri, 3.7);
    float3 P = ri.P + 0.9 * (wn.rgb - 0.5);
    float2 sw = float2(1.0 / tile, 1.0 / (3.0 * tile));          // walls: (horizontal, vertical) scale, 3:1 runs
    float sf = 1.0 / tile;
    float gx = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, P.zy * sw + offs, ri.dPdx.zy * sw, ri.dPdy.zy * sw).g;
    float gy = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, P.xz * sf + offs, ri.dPdx.xz * sf, ri.dPdy.xz * sf).g;
    float gz = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, P.xy * sw + offs, ri.dPdx.xy * sw, ri.dPdy.xy * sw).g;
    return gx * w.x + gy * w.y + gz * w.z;
}

// Deck plate tone of a steel walking surface (R2-14): 1.2 x 2.4 m plates (seams at x = 0.6 + 1.2 k, so the 1.2 m bridge
// deck has its seams at its edges, and every 2.4 m across), per-plate tone in [-1, 1] box-filtered across the x seams.
float AbDeckPlateTone(float3 P, float fwX)
{
    float2 pp = float2((P.x - 0.6) / 1.2, P.z / 2.4);
    int2 pc = (int2)floor(pp);
    float2 wx = AbCellBlend(pp.x, fwX / 1.2);
    int2 n = int2(1110, 555);                                     // 1332 / 1.2, 1332 / 2.4
    float t = AbHash2(pc, n, 173u) * (1.0 - wx.x - wx.y) + AbHash2(pc - int2(1, 0), n, 173u) * wx.x
            + AbHash2(pc + int2(1, 0), n, 173u) * wx.y;
    return 2.0 * t - 1.0;
}

AbSurface AbRecipeSteel(AbRecipeInput ri, float weathered)
{
    AbSurface s = AbDefaultSurface(ri.N);
    float4 n3 = AbNoiseTriplanar(ri, 3.0);
    float4 n1 = AbNoiseTriplanar(ri, 0.75);
    float footprint = max(length(ri.dPdx), length(ri.dPdy));
    float edgeG = saturate(ri.color.g);
    float walk = smoothstep(0.7, 0.9, ri.N.y);                              // horizontal walking surfaces

    float3 nBev = s.normalWS;
    float kBevel = AbRecipeBevel(ri, nBev);
    s.normalWS = nBev;

    // patch value: warped, run-shaped structure patches; on walking surfaces stochastic per-6 m-cell lookups (AbBombG: a
    // random offset and rotation per cell, so no stain shape repeats every 4.5-7.4 m as the R1 "paw prints" did)
    float coarse = AbUniformMax2(AbRustPatchG(ri, AB_PATCH_TILE_A, float2(0.0, 0.0)), AbRustPatchG(ri, AB_PATCH_TILE_B, float2(0.31, 0.57)));
    if (walk > 0.0)
    {
        float deck = AbUniformMax2(AbBombG(ri.P.xz, ri.dPdx.xz, ri.dPdy.xz, 7.4, 6.0, 222, 191u),
                                   AbBombG(ri.P.xz + 3.0, ri.dPdx.xz, ri.dPdy.xz, 4.44, 6.0, 222, 193u));
        coarse = lerp(coarse, deck, walk);
    }
    coarse = saturate(coarse + AB_RUST_EDGE * edgeG);
    float rust = AbRustMask(_RustAmount, coarse, n1.g, footprint);
    float halo = (1.0 - rust) * 0.15 * AbSmoothstepSafe(1.0 - 4.0 * _RustAmount, 1.0 - _RustAmount, coarse) * step(1e-4, _RustAmount)
               * (1.0 - smoothstep(0.03, 0.15, footprint));
    // edge wear broken up by the 3 m channel (the 0.75 m channel turned whole G-faces into bare-steel "granite" speckle)
    float wear = saturate(edgeG * _WearAmount * (0.6 + 0.8 * n3.b)) * kBevel;

    // grime: vertical runs (1.5 x 6 m lattice) and broad patches (4.5 m), about +/-9 % on weathered steel
    float runs = AbValueNoise3N(float3(ri.P.x, ri.P.y * 0.25, ri.P.z), 1.5, int3(888, 222, 888), 131u);
    float broad = AbValueNoise3(ri.P, 4.5, 296, 137u);
    float grime = lerp(1.0, 1.0 - 0.10 * runs - 0.08 * broad + 0.09, max(weathered, 0.6 * walk));
    // walking decks (R1-28, R2-14): scuffs (StreakTex.g along both plan axes, +/-8 %), anti-slip grit (+/-6 %), 1.2 x 2.4 m
    // deck plates (+/-8 % tone, +/-0.06 roughness; lead_b01 KS5c deck at 20-25 levels: +/-5 % was about 1 level), 8 mm weld seams held at 1.5 px (-30 %) with a grime band (24 cm,
    // -10 %) along them: the lift and bridge decks read as worn steel plate, not as one graybox plane
    float scuff = 0.0;
    float plate = 0.0;
    float seam = 0.0;
    if (walk > 0.0)
    {
        float2 sx = ri.P.xz / 1.2;
        float ga = SAMPLE_TEXTURE2D_GRAD(_AbStreakTex, sampler_AbStreakTex, sx, ri.dPdx.xz / 1.2, ri.dPdy.xz / 1.2).g;
        float gb = SAMPLE_TEXTURE2D_GRAD(_AbStreakTex, sampler_AbStreakTex, sx.yx * 0.5 + 0.21, ri.dPdx.zx * 0.5 / 1.2, ri.dPdy.zx * 0.5 / 1.2).g;
        scuff = walk * (0.16 * (0.5 * (ga + gb) - 0.5) + 0.12 * (n1.a - 0.5));     // anti-slip grit +/-6 % (KS2a deck high-pass)
        float fwX = AbFootprint(ri.dPdx, ri.dPdy, float3(1.0, 0.0, 0.0));
        float fwZ = AbFootprint(ri.dPdx, ri.dPdy, float3(0.0, 0.0, 1.0));
        plate = walk * AbDeckPlateTone(ri.P, fwX) * AbFeatureFade(1.2 / max(max(fwX, fwZ), 1e-6) / 2.0);
        float kx, kz, kgx, kgz;
        float sxl = AbLineMinWidth(ri.P.x - 0.6, 1.2, 0.008, fwX, 1.5, kx);
        float szl = AbLineMinWidth(ri.P.z, 2.4, 0.008, fwZ, 1.5, kz);
        float gxl = AbLineMinWidth(ri.P.x - 0.6, 1.2, 0.24, fwX, 1.5, kgx);
        float gzl = AbLineMinWidth(ri.P.z, 2.4, 0.24, fwZ, 1.5, kgz);
        seam = walk * (1.0 - (1.0 - 0.30 * sxl * kx) * (1.0 - 0.30 * szl * kz) * (1.0 - 0.10 * gxl * kgx) * (1.0 - 0.10 * gzl * kgz));
    }
    float3 paint = _BaseColor.rgb * grime * (1.0 + 0.04 * (n3.b - 0.5) + scuff) * (1.0 + 0.08 * plate) * (1.0 - seam);
    float3 bare = float3(0.20, 0.21, 0.22);
    float3 rustCol = lerp(AbRustColor(paint, n1.a), AbStainColor(paint, n1.a), walk);
    float3 albedo = lerp(paint, bare, wear);
    albedo = lerp(albedo, rustCol, saturate(rust + halo));
    s.albedo = albedo;
    // R2-10: on walking surfaces a stain keeps most of the paint's metallic and gloss (rough +0.05): the low beat-6 Key's
    // sheen on the paint against a matte, non-metallic stain made the 8-14 % albedo stains read 50 % darker (r31 B6_start
    // blots 61 against 88 levels); structural rust stays matte and non-metallic
    s.metallic = lerp(lerp(_Metallic, 0.85, wear), lerp(0.0, 0.85 * _Metallic, walk), rust);
    float paintRough = max(_Roughness + 0.06 * (n3.r - 0.5) - 0.5 * scuff + 0.06 * plate + 0.15 * seam, 0.25);
    // rust and deck grime are both matte: a glossy stain under the low beat-6 Key read as white puddles (r01)
    float stainRough = lerp(0.88, min(paintRough + 0.05, 1.0), walk);
    s.roughness = lerp(lerp(paintRough, 0.38, wear), stainRough, saturate(rust + halo));
    s.roughness = lerp(max(s.roughness, 0.7), s.roughness, lerp(1.0, kBevel, smoothstep(0.85, 0.97, edgeG)));
    s.ao = saturate(ri.color.r);
    return s;
}

AbSurface AbRecipeRod(AbRecipeInput ri)
{
    AbSurface s = AbDefaultSurface(ri.N);
    float4 n1 = AbNoiseTriplanar(ri, 3.0);
    s.albedo = _BaseColor.rgb * (1.0 + 0.04 * (n1.r - 0.5));
    s.metallic = 1.0;
    s.roughness = max(_Roughness + 0.04 * (n1.g - 0.5), 0.30);
    s.ao = saturate(ri.color.r);
    return s;
}

AbSurface AbRecipeCable(AbRecipeInput ri)
{
    AbSurface s = AbDefaultSurface(ri.N);
    s.albedo = _BaseColor.rgb;
    s.metallic = 0.0;
    s.roughness = max(_Roughness, 0.5);
    s.ao = saturate(ri.color.r);
    return s;
}

AbSurface AbRecipeDoorLeaf(AbRecipeInput ri)
{
    AbSurface s = AbDefaultSurface(ri.N);
    float4 n1 = AbNoiseTriplanar(ri, 0.75);
    float3 nBev = s.normalWS;
    float kBevel = AbRecipeBevel(ri, nBev);
    s.normalWS = nBev;
    float wear = saturate(ri.color.g) * _WearAmount * kBevel;
    s.albedo = _BaseColor.rgb * (1.0 + 0.06 * (n1.r - 0.5)) * (1.0 + 0.5 * wear);
    s.metallic = lerp(_Metallic, 0.7, wear);
    s.roughness = lerp(max(_Roughness + 0.06 * (n1.g - 0.5), 0.3), 0.4, wear);
    s.ao = saturate(ri.color.r);
    return s;
}

// Honeycomb tube walls: board-formed concrete, albedo x exp(-depth / _DepthFalloff) below _TopY (14.5.7), domain 2.
// R2-19: a per-cell +/-6 % tone (AbHoneyCellTone, the same hash as the tier-2 Lattice) and worn edges (vertex G, e.g. the
// rim chamfers) darkened by grime instead of lightened, so the rims are not a bright cookie-cutter lip. The tube air and
// the aerial veil are applied after lighting (Surface.shader, AbHoneyAir).
AbSurface AbRecipeHoneyTube(AbRecipeInput ri)
{
    AbSurface s = AbRecipeConcreteBoard(ri);
    float depth = max(_TopY - ri.positionWS.y, 0.0);
    s.albedo *= exp(-depth / max(_DepthFalloff, 1.0));
    s.albedo *= AbHoneyCellTone(ri.positionWS.xz, float2(AB_HONEY_ORIGIN_X, AB_HONEY_ORIGIN_Z), AB_HONEY_SIZE);
    s.albedo *= 1.0 - 0.3 * saturate(saturate(ri.color.g) * _WearAmount);   // grimed rims (net -12 % resolved, -30 % sub-pixel)
    return s;
}

// Uniform dispatch on the material's recipe id.
AbSurface AbEvaluateRecipe(int recipe, AbRecipeInput ri)
{
    AbSurface s;
    [branch] if (recipe == AB_RECIPE_CONCRETE_FLOOR)      s = AbRecipeConcreteFloor(ri);
    else if (recipe == AB_RECIPE_CONCRETE_CYL)            s = AbRecipeConcreteCyl(ri);
    else if (recipe == AB_RECIPE_STEEL_PAINTED)           s = AbRecipeSteel(ri, 0.0);
    else if (recipe == AB_RECIPE_STEEL_WEATHERED)         s = AbRecipeSteel(ri, 1.0);
    else if (recipe == AB_RECIPE_ROD_POLISHED)            s = AbRecipeRod(ri);
    else if (recipe == AB_RECIPE_CABLE)                   s = AbRecipeCable(ri);
    else if (recipe == AB_RECIPE_DOOR_LEAF)               s = AbRecipeDoorLeaf(ri);
    else if (recipe == AB_RECIPE_HONEY_TUBE)              s = AbRecipeHoneyTube(ri);
    else                                                  s = AbRecipeConcreteBoard(ri);
    return s;
}

#endif // ABYSS_SURFACE_RECIPES_INCLUDED
