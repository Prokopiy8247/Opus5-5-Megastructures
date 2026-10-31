using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Megastructure
{
    /// <summary>
    /// The single directional "Key" light of 14.2, driven by ProgressDirector (beats never touch it).
    ///
    /// The wanted state is a row of <see cref="ProgressTables.KeyRows"/> chosen by E. When only the intensity changes
    /// (<see cref="KeyRow.SameLook"/>) the rig adopts the new row at once and eases the intensity, the scatter amount and the
    /// haze skylight from the old row's values to the new row's over 3 s (no dip); when the direction, colour, shadow distance
    /// or shaft-depth falloff changes it first ramps to 0 over 3 s, switches direction, colour, the URP shadow distance and
    /// (through <see cref="ActiveRow"/>, read by ProgressDirector for _AbKeyDepth) the depth falloff while dark (so cascades
    /// never refit and the falloff never jumps visibly), then ramps up over 3 s. All ramps are smootherstep in time. While the
    /// level is 0 the Light component is disabled, so no shadow map is rendered in the dark beats (1, 4, tunnel). Plain class,
    /// no per-frame allocation.
    /// </summary>
    public sealed class KeyLightRig
    {
        readonly Light _light;
        readonly UniversalRenderPipelineAsset _urp;
        readonly float _originalShadowDistance;

        int _activeRow = ProgressTables.KEY_OFF;   // row whose direction / colour / shadow distance the light carries
        int _blendFromRow = ProgressTables.KEY_OFF; // previous row of an intensity-only change (same look), eased out with the ramp
        float _level;                              // current intensity
        float _rampFrom, _rampTo, _rampU = 1f;

        /// <summary>Current intensity (0 = off).</summary>
        public float Level => _level;

        /// <summary>Row whose look (direction, colour, distance) is currently applied.</summary>
        public int ActiveRow => _activeRow;

        /// <summary>Eased weight of the active row against the previous row of an intensity-only change (1 = settled).</summary>
        float RowWeight => _blendFromRow == _activeRow || _rampU >= 1f ? 1f : ProgressTables.Smootherstep(_rampU);

        /// <summary>Full intensity the light is heading for: eased between the rows of an intensity-only change.</summary>
        float FullIntensity
        {
            get
            {
                float to = ProgressTables.KeyRows[_activeRow].Intensity;
                float from = ProgressTables.KeyRows[_blendFromRow].Intensity;
                return Mathf.Lerp(from, to, RowWeight);
            }
        }

        /// <summary>
        /// Level relative to the full intensity (0..1), used to fade the scatter lobe and the haze skylight with the light. During an
        /// intensity-only change the reference eases with the level, so the fraction stays 1 (no pop of the scatter lobe).
        /// </summary>
        public float Fraction
        {
            get
            {
                float full = FullIntensity;
                return full > 1e-6f ? Mathf.Clamp01(_level / full) : 0f;
            }
        }

        /// <summary>Domain-0 HG scatter amount to write (_AbScatterAmt): the row values eased like the intensity, times <see cref="Fraction"/>.</summary>
        public float ScatterAmount
        {
            get
            {
                float to = ProgressTables.KeyRows[_activeRow].ScatterAmt, from = ProgressTables.KeyRows[_blendFromRow].ScatterAmt;
                return Mathf.Lerp(from, to, RowWeight) * Fraction;
            }
        }

        /// <summary>Haze skylight gain (KeyRow.HazeSky) eased like the intensity, times <see cref="Fraction"/>.</summary>
        public float HazeSky
        {
            get
            {
                float to = ProgressTables.KeyRows[_activeRow].HazeSky, from = ProgressTables.KeyRows[_blendFromRow].HazeSky;
                return Mathf.Lerp(from, to, RowWeight) * Fraction;
            }
        }

        /// <param name="light">The Key light (may be null in tests).</param>
        /// <param name="controlShadowDistance">Write the URP shadow distance per row (false in EditMode tests: the asset is never touched).</param>
        public KeyLightRig(Light light, bool controlShadowDistance = true)
        {
            _light = light;
            _urp = controlShadowDistance ? GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset : null;
            _originalShadowDistance = _urp != null ? _urp.shadowDistance : 0f;
            if (_light != null)
            {
                _light.type = LightType.Directional;
                _light.shadows = LightShadows.Soft;
                _light.shadowStrength = 1f;
                _light.intensity = 0f;
                _light.enabled = false;
            }
        }

        /// <summary>Advance the ramps toward the wanted row by dt seconds (0 while paused).</summary>
        public void Update(int wantRow, float dt)
        {
            ref readonly KeyRow want = ref ProgressTables.KeyRows[wantRow];
            ref readonly KeyRow active = ref ProgressTables.KeyRows[_activeRow];

            float goal;
            bool adopt = false;
            if (!want.On) goal = 0f;                                    // turning off: ramp down, keep the look
            else if (want.SameLook(active))
            {
                goal = want.Intensity;                                  // intensity-only change: adopt the row, ease, no dip
                adopt = wantRow != _activeRow;
            }
            else if (_level <= 0f)
            {
                _activeRow = wantRow;                                   // dark: switch direction, colour, shadow distance
                _blendFromRow = wantRow;
                ApplyLook();
                goal = want.Intensity;
            }
            else goal = 0f;                                             // look differs: dip to 0 first

            if (adopt)
            {
                // the per-row values (scatter, haze skylight, reference intensity) ease from the old row with the same ramp
                _blendFromRow = _activeRow;
                _activeRow = wantRow;
                ApplyLook();
            }
            if (goal != _rampTo || adopt)
            {
                if (!adopt) _blendFromRow = _activeRow;   // a dip, a switch-off or a rise after a switch never blends rows
                _rampFrom = _level; _rampTo = goal; _rampU = 0f;
            }
            if (_rampU >= 1f) _blendFromRow = _activeRow;
            if (_rampU < 1f)
            {
                _rampU = Mathf.Min(1f, _rampU + dt / ProgressTables.KEY_RAMP_SECONDS);
                _level = _rampU >= 1f ? _rampTo : Mathf.Lerp(_rampFrom, _rampTo, ProgressTables.Smootherstep(_rampU));
            }
            ApplyLevel();
        }

        /// <summary>Jump straight to the wanted row at full intensity (after teleports, for captures).</summary>
        public void Snap(int wantRow)
        {
            ref readonly KeyRow want = ref ProgressTables.KeyRows[wantRow];
            if (want.On)
            {
                _activeRow = wantRow;
                ApplyLook();
            }
            _blendFromRow = _activeRow;
            _level = want.On ? want.Intensity : 0f;
            _rampFrom = _rampTo = _level;
            _rampU = 1f;
            ApplyLevel();
        }

        /// <summary>Restore the pipeline asset's shadow distance (in the Editor the asset would otherwise stay modified).</summary>
        public void Dispose()
        {
            if (_urp != null) _urp.shadowDistance = _originalShadowDistance;
        }

        void ApplyLook()
        {
            ref readonly KeyRow r = ref ProgressTables.KeyRows[_activeRow];
            if (!r.On) return;
            if (_light != null)
            {
                _light.transform.rotation = AbyssConst.YawPitch(r.Yaw, r.Pitch);
                _light.color = r.Colour;
            }
            if (_urp != null) _urp.shadowDistance = r.ShadowDistance;
        }

        void ApplyLevel()
        {
            if (_light == null) return;
            bool on = _level > 0f;
            if (_light.enabled != on) _light.enabled = on;
            _light.intensity = _level;
        }
    }
}
