using System;

namespace Megastructure.Audio
{
    // =====================================================================================================================
    // Layers (continuous beds) and voices (seeded one-shot events) of the 14.3 score. Unity-free, allocation-free after
    // construction. Every layer renders one control block (<= 32 samples) at a time into an AudioBus, with its gain
    // linearly ramped across the block (the engine smooths the targets with a 50 ms one-pole at control rate), so no
    // parameter change can produce a step. Beds are normalised so gain 1 gives 0 dBFS RMS per channel; voices so gain 1
    // gives a 0 dBFS dry peak (calibrated by rendering at engine construction).
    // =====================================================================================================================

    /// <summary>Per-block mix buses: the faded bus (layers, events, footsteps) and the unfaded bus (door thud, end tone).</summary>
    public sealed class AudioBus
    {
        public const int Size = 32;
        public readonly float[] DryL = new float[Size], DryR = new float[Size], SendL = new float[Size], SendR = new float[Size];
        public readonly float[] UDryL = new float[Size], UDryR = new float[Size], USendL = new float[Size], USendR = new float[Size];

        public void Clear()
        {
            Array.Clear(DryL, 0, Size); Array.Clear(DryR, 0, Size); Array.Clear(SendL, 0, Size); Array.Clear(SendR, 0, Size);
            Array.Clear(UDryL, 0, Size); Array.Clear(UDryR, 0, Size); Array.Clear(USendL, 0, Size); Array.Clear(USendR, 0, Size);
        }

        /// <summary>Add a mono sample with pan gains, a dry multiplier and a send multiplier to the faded bus.</summary>
        public void Add(int i, double v, float gl, float gr, float dry, float send)
        {
            float l = (float)(v * gl), r = (float)(v * gr);
            DryL[i] += l * dry; DryR[i] += r * dry;
            SendL[i] += l * send; SendR[i] += r * send;
        }

        public void AddUnfaded(int i, double v, float gl, float gr, float dry, float send)
        {
            float l = (float)(v * gl), r = (float)(v * gr);
            UDryL[i] += l * dry; UDryR[i] += r * dry;
            USendL[i] += l * send; USendR[i] += r * send;
        }
    }

    // ------------------------------------------------------------------------------------------------------------- beds

    /// <summary>
    /// Stereo noise bed: independent mid and side noise sources through identical filters, L = m + w s, R = m - w s
    /// (gentle decorrelation; the mono sum is exactly 2 m, so nothing cancels). Normalised from the impulse-response
    /// energy of the colour + filter chain (exact, no statistical error).
    /// </summary>
    public sealed class NoiseBed
    {
        readonly NoiseSource _mid, _side;
        readonly Svf _fm, _fs;
        readonly OnePoleHp _hm, _hs;
        readonly SvfMode _mode;
        readonly double _q, _rate, _width, _norm;
        public readonly double NominalFc;
        public float Send;

        public NoiseBed(NoiseColour colour, uint seed, SvfMode mode, double fc, double q, double hpHz, double width, float send, double rate)
        {
            _rate = rate; _mode = mode; _q = q; _width = width; Send = send; NominalFc = fc;
            _mid = new NoiseSource(colour, DspMath.MixSeed(seed, 1u), rate);
            _side = new NoiseSource(colour, DspMath.MixSeed(seed, 2u), rate);
            _fm = new Svf(fc, q, rate);
            _fs = new Svf(fc, q, rate);
            if (hpHz > 0.0) { _hm = new OnePoleHp(hpHz, rate); _hs = new OnePoleHp(hpHz, rate); }
            double energy = ImpulseEnergy(colour, mode, fc, q, hpHz, rate);
            _norm = 1.0 / Math.Sqrt(NoiseSource.WhiteVariance * energy * (1.0 + width * width));
        }

        /// <summary>Sum of h[n]^2 of colour filter -> SVF -> optional HP for a unit impulse (2^17 samples).</summary>
        public static double ImpulseEnergy(NoiseColour colour, SvfMode mode, double fc, double q, double hpHz, double rate)
        {
            var src = new NoiseSource(colour, 1u, rate);
            var f = new Svf(fc, q, rate);
            OnePoleHp hp = hpHz > 0.0 ? new OnePoleHp(hpHz, rate) : null;
            double e = 0.0;
            for (int n = 0; n < 131072; n++)
            {
                double x = src.Shape(n == 0 ? 1.0 : 0.0);
                double y = f.Process(x, mode);
                if (hp != null) y = hp.Process(y);
                e += y * y;
            }
            return e;
        }

        public void SetCutoff(double fc) { _fm.Set(fc, _q, _rate); _fs.Set(fc, _q, _rate); }

        /// <summary>Render one block; g0/g1 are the linear gains at the block start and end (ramped).</summary>
        public void Render(AudioBus b, int n, double g0, double g1)
        {
            double dg = (g1 - g0) / n, g = g0, w = _width, k = _norm;
            bool hp = _hm != null;
            for (int i = 0; i < n; i++)
            {
                g += dg;
                double m = _fm.Process(_mid.Next(), _mode);
                double s = _fs.Process(_side.Next(), _mode);
                if (hp) { m = _hm.Process(m); s = _hs.Process(s); }
                double gk = g * k;
                float l = (float)((m + w * s) * gk), r = (float)((m - w * s) * gk);
                b.DryL[i] += l; b.DryR[i] += r;
                b.SendL[i] += l * Send; b.SendR[i] += r * Send;
            }
        }
    }

    /// <summary>
    /// SUB (14.3): sines 31.0 and 41.2 Hz, each with a quieter partner detuned by 0.2 +/- 0.15 Hz (0.05 Hz LFO, so the
    /// pair beats at about 0.2 Hz), amplitude LFO 0.03 Hz with 30 % depth (applied by the engine through the gain), and
    /// the 62 / 93 Hz harmonics of the 31 Hz tone from an asymmetric tanh soft saturation (DC-blocked).
    /// </summary>
    public sealed class SubLayer
    {
        public const double F1 = 31.0, F2 = 41.2, PartnerAmp = 0.3, HarmonicMix = 0.8; // 62 / 93 Hz at -16 / -17.5 dB re 31 Hz
        public const double DetuneLfoHz = 0.05, DetuneBase = 0.2, DetuneDepth = 0.15;
        public const double AmLfoHz = 0.03, AmDepth = 0.3;
        const double SatDrive = 1.6, SatBias = 0.35;

        readonly SineOsc _a = new SineOsc(), _ad = new SineOsc(), _b = new SineOsc(), _bd = new SineOsc();
        readonly DcBlocker _hdc;
        readonly double _rate, _norm, _incA, _incB;
        static readonly double SatLin = SatDrive * (1.0 - Math.Tanh(SatBias) * Math.Tanh(SatBias));
        static readonly double SatOff = Math.Tanh(SatBias);
        double _incAd, _incBd;
        public float Send = 0.2f;

        public SubLayer(double rate)
        {
            _rate = rate;
            _incA = F1 / rate; _incB = F2 / rate;
            _hdc = new DcBlocker(8.0, rate);
            _bd.Phase = 0.37; // decorrelate the two pairs' beat nulls
            // power: two tones of (1 + p^2)/2, the DC-free harmonic residue (averaged over one cycle), the AM LFO mean square
            double ph = 0.0, ph2 = 0.0;
            const int M = 4096;
            for (int i = 0; i < M; i++)
            {
                double h = Harmonics(Math.Sin(DspMath.TwoPi * i / M)) * HarmonicMix;
                ph += h; ph2 += h * h;
            }
            ph /= M; ph2 = ph2 / M - ph * ph;
            double am = 1.0 - AmDepth * 0.5, amVar = AmDepth * 0.5;
            double amMs = am * am + amVar * amVar * 0.5;
            _norm = 1.0 / Math.Sqrt(((1.0 + PartnerAmp * PartnerAmp) + ph2) * amMs);
            Control(0.0);
        }

        /// <summary>Distortion residue of the asymmetric soft saturation (its linear part removed): 62 + 93 Hz content.</summary>
        public static double Harmonics(double x) => Math.Tanh(SatDrive * x + SatBias) - SatOff - SatLin * x;

        /// <summary>Amplitude LFO factor at LFO time t (0.7 .. 1.0).</summary>
        public static double AmFactor(double t) => 1.0 - AmDepth * (0.5 + 0.5 * Math.Sin(DspMath.TwoPi * AmLfoHz * t));

        public void Control(double t)
        {
            double d1 = DetuneBase + DetuneDepth * Math.Sin(DspMath.TwoPi * DetuneLfoHz * t);
            double d2 = DetuneBase + DetuneDepth * Math.Sin(DspMath.TwoPi * DetuneLfoHz * t + 1.9);
            _incAd = (F1 + d1) / _rate;
            _incBd = (F2 + d2) / _rate;
        }

        public void Render(AudioBus b, int n, double g0, double g1)
        {
            double dg = (g1 - g0) / n, g = g0, k = _norm;
            for (int i = 0; i < n; i++)
            {
                g += dg;
                double sa = _a.Next(_incA);
                double x = sa + PartnerAmp * _ad.Next(_incAd) + _b.Next(_incB) + PartnerAmp * _bd.Next(_incBd);
                x += HarmonicMix * _hdc.Process(Harmonics(sa));
                float v = (float)(x * g * k);
                b.DryL[i] += v; b.DryR[i] += v;
                float s = v * Send;
                b.SendL[i] += s; b.SendR[i] += s;
            }
        }
    }

    /// <summary>HUM (14.3): 50 Hz plus 100 (-6 dB), 150 (-10 dB), 250 (-18 dB); the 5 % 0.2 Hz wobble comes through the gain.</summary>
    public sealed class HumLayer
    {
        public const double Fundamental = 50.0, WobbleHz = 0.2, WobbleDepth = 0.05;
        static readonly double A2 = Math.Pow(10.0, -6.0 / 20.0), A3 = Math.Pow(10.0, -10.0 / 20.0), A5 = Math.Pow(10.0, -18.0 / 20.0);
        readonly double _inc, _norm;
        double _phase;
        public float Send = 0.5f;

        public HumLayer(double rate)
        {
            _inc = Fundamental / rate;
            double p = (1.0 + A2 * A2 + A3 * A3 + A5 * A5) * 0.5 * (1.0 + WobbleDepth * WobbleDepth * 0.5);
            _norm = 1.0 / Math.Sqrt(p);
        }

        public static double Wobble(double t) => 1.0 + WobbleDepth * Math.Sin(DspMath.TwoPi * WobbleHz * t);

        public void Render(AudioBus b, int n, double g0, double g1)
        {
            double dg = (g1 - g0) / n, g = g0, k = _norm;
            for (int i = 0; i < n; i++)
            {
                g += dg;
                double th = DspMath.TwoPi * _phase;
                _phase += _inc; if (_phase >= 1.0) _phase -= 1.0;
                double s1 = Math.Sin(th), c1 = Math.Cos(th);
                double s2 = 2.0 * s1 * c1, c2 = c1 * c1 - s1 * s1;
                double s3 = s2 * c1 + c2 * s1, c3 = c2 * c1 - s2 * s1;
                double s5 = s3 * c2 + c3 * s2;
                float v = (float)((s1 + A2 * s2 + A3 * s3 + A5 * s5) * g * k);
                b.DryL[i] += v; b.DryR[i] += v;
                float s = v * Send;
                b.SendL[i] += s; b.SendR[i] += s;
            }
        }
    }

    /// <summary>Pure tone layer (lift motor whine with two weak harmonics, or the 31 Hz ending tone on the unfaded bus).</summary>
    public sealed class ToneLayer
    {
        readonly double _rate, _h2, _h3, _norm;
        readonly bool _unfaded;
        double _phase, _inc;
        public float Send;

        public ToneLayer(double freq, double h2, double h3, float send, bool unfaded, double rate)
        {
            _rate = rate; _h2 = h2; _h3 = h3; Send = send; _unfaded = unfaded;
            _norm = 1.0 / Math.Sqrt((1.0 + h2 * h2 + h3 * h3) * 0.5);
            SetFrequency(freq);
        }

        public void SetFrequency(double f) { _inc = f / _rate; }

        public void Render(AudioBus b, int n, double g0, double g1)
        {
            double dg = (g1 - g0) / n, g = g0, k = _norm;
            for (int i = 0; i < n; i++)
            {
                g += dg;
                double th = DspMath.TwoPi * _phase;
                _phase += _inc; if (_phase >= 1.0) _phase -= 1.0;
                double s1 = Math.Sin(th);
                double x = s1;
                if (_h2 != 0.0 || _h3 != 0.0)
                {
                    double c1 = Math.Cos(th);
                    double s2 = 2.0 * s1 * c1, c2 = c1 * c1 - s1 * s1;
                    x += _h2 * s2 + _h3 * (s2 * c1 + c2 * s1);
                }
                float v = (float)(x * g * k);
                if (_unfaded) { b.UDryL[i] += v; b.UDryR[i] += v; b.USendL[i] += v * Send; b.USendR[i] += v * Send; }
                else { b.DryL[i] += v; b.DryR[i] += v; b.SendL[i] += v * Send; b.SendR[i] += v * Send; }
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------ voices

    /// <summary>Base for one-shot voices: a calibrated peak normaliser, pan gains, dry/send multipliers, unfaded flag.</summary>
    public abstract class Voice
    {
        protected readonly double Rate;
        public double Norm = 1.0;
        public bool Active;
        protected float Gl = 1f, Gr = 1f;
        public float Dry = 1f, Send = 1f;
        public bool Unfaded;

        protected Voice(double rate) { Rate = rate; }

        /// <summary>Next mono sample before normalisation; sets <see cref="Active"/> false when finished.</summary>
        public abstract double NextRaw();

        public void Render(AudioBus b, int n, double g0, double g1)
        {
            double dg = (g1 - g0) / n, g = g0;
            for (int i = 0; i < n && Active; i++)
            {
                g += dg;
                double v = NextRaw() * Norm * g;
                if (Unfaded) b.AddUnfaded(i, v, Gl, Gr, Dry, Send);
                else b.Add(i, v, Gl, Gr, Dry, Send);
            }
        }

        protected void SetPan(double pan) { DspMath.Pan(pan, out Gl, out Gr); }

        /// <summary>Render the voice alone (already triggered) and return 1 / peak, for calibration at construction.</summary>
        public double MeasureInversePeak(int maxSamples)
        {
            double peak = 1e-12;
            for (int i = 0; i < maxSamples && Active; i++)
            {
                double v = Math.Abs(NextRaw());
                if (v > peak) peak = v;
            }
            Active = false;
            return 1.0 / peak;
        }
    }

    /// <summary>MACHINE PULSE (14.3): 38 Hz sine burst (attack 20 ms, decay 1.4 s) plus 90-250 Hz noise (decay 0.6 s).</summary>
    public sealed class PulseVoice : Voice
    {
        public const double Freq = 38.0, SineAttack = 0.020, SineDecay = 1.4, NoiseAttack = 0.005, NoiseDecay = 0.6;
        const double NoiseMix = 3.0;
        readonly SineOsc _osc = new SineOsc();
        readonly AdEnvelope _es = new AdEnvelope(), _en = new AdEnvelope();
        readonly Svf _bp;
        Xorshift32 _rng;
        readonly double _inc;

        public PulseVoice(double rate) : base(rate)
        {
            _es.Set(SineAttack, SineDecay, rate);
            _en.Set(NoiseAttack, NoiseDecay, rate);
            _bp = new Svf(Math.Sqrt(90.0 * 250.0), Math.Sqrt(90.0 * 250.0) / 160.0, rate);
            _inc = Freq / rate;
            Send = 1.2f;
        }

        public void Trigger(uint seed, double pan)
        {
            _osc.Phase = 0.0; _es.Trigger(); _en.Trigger(); _bp.Reset(); _rng = new Xorshift32(seed);
            SetPan(pan); Active = true;
        }

        public override double NextRaw()
        {
            double s = _osc.Next(_inc) * _es.Next();
            double nz = _bp.Process(_rng.NextBipolar(), SvfMode.BandPassNormalized) * _en.Next() * NoiseMix;
            if (!_es.Active && !_en.Active) Active = false;
            return s + nz;
        }
    }

    /// <summary>
    /// GROAN (14.3): two triangle partials gliding 74 -> 61 Hz over 6 s through a resonant band-pass (220 Hz centre,
    /// Q 6) swept 160 -> 300 -> 160 Hz, plus a little low body; slow 1.2 s swell and 2.5 s release; drifts across the
    /// stereo field (seeded direction).
    /// </summary>
    public sealed class GroanVoice : Voice
    {
        public const double Duration = 6.0, F0 = 74.0, F1 = 61.0, Q = 6.0, SweepLo = 160.0, SweepHi = 300.0;
        const double Partial2 = 2.01, Partial2Amp = 0.55, BodyMix = 0.35;
        readonly TriangleOsc _t1 = new TriangleOsc(), _t2 = new TriangleOsc();
        readonly Svf _bp, _body;
        readonly double _glide;
        double _t, _dt, _pan0, _pan1;
        int _ctl;

        public GroanVoice(double rate) : base(rate)
        {
            _bp = new Svf(SweepLo, Q, rate);
            _body = new Svf(180.0, 0.7, rate);
            _glide = Math.Log(F1 / F0) / Duration;
            _dt = 1.0 / rate;
            Send = 1.5f;
        }

        public void Trigger(uint seed)
        {
            var r = new Xorshift32(seed);
            double side = r.NextUnit() < 0.5 ? -1.0 : 1.0;
            _pan0 = side * (0.15 + 0.2 * r.NextUnit());
            _pan1 = -side * (0.1 + 0.2 * r.NextUnit());
            _t1.Phase = 0.25; _t2.Phase = 0.25 + 0.5 * r.NextUnit();
            _bp.Reset(); _body.Reset(); _t = 0.0; _ctl = 0;
            SetPan(_pan0); Active = true;
        }

        public override double NextRaw()
        {
            if (_t >= Duration) { Active = false; return 0.0; }
            if (--_ctl <= 0)
            {
                _ctl = 32;
                double u = _t / Duration;
                _bp.Set(SweepLo + (SweepHi - SweepLo) * Math.Sin(Math.PI * u), Q, Rate);
                SetPan(_pan0 + (_pan1 - _pan0) * u);
            }
            double f = F0 * Math.Exp(_glide * _t);
            double x = _t1.Next(f / Rate) + Partial2Amp * _t2.Next(Partial2 * f / Rate);
            double y = _bp.Process(x, SvfMode.BandPass) + BodyMix * _body.Process(x, SvfMode.LowPass);
            double env = DspMath.Smoothstep(_t / 1.2) * (1.0 - DspMath.Smoothstep((_t - 3.5) / 2.5));
            _t += _dt;
            return y * env;
        }
    }

    /// <summary>BELL (14.3): modes 83, 137, 211, 283 Hz decaying 5, 4, 3, 2 s, struck by a 4 ms half-sine mallet.</summary>
    public sealed class BellVoice : Voice
    {
        public static readonly double[] ModeHz = { 83.0, 137.0, 211.0, 283.0 };
        public static readonly double[] ModeT60 = { 5.0, 4.0, 3.0, 2.0 };
        static readonly double[] ModeAmp = { 1.0, 0.7, 0.5, 0.35 };
        const double MalletSeconds = 0.004, Life = 6.0;
        readonly ModalResonator[] _m = new ModalResonator[4];
        readonly int _malletLen;
        int _n;
        readonly int _lifeSamples;

        public BellVoice(double rate) : base(rate)
        {
            for (int i = 0; i < 4; i++) _m[i] = new ModalResonator();
            _malletLen = Math.Max(4, (int)(MalletSeconds * rate));
            _lifeSamples = (int)(Life * rate);
            Dry = 0.5f; Send = 4.0f; // "mostly reverb"
        }

        public void Trigger(uint seed)
        {
            var r = new Xorshift32(seed);
            double detune = 1.0 + 0.004 * (2.0 * r.NextUnit() - 1.0);
            for (int i = 0; i < 4; i++) { _m[i].Set(ModeHz[i] * detune, ModeT60[i], ModeAmp[i], Rate); _m[i].Reset(); }
            SetPan(0.4 * (2.0 * r.NextUnit() - 1.0));
            _n = 0; Active = true;
        }

        public override double NextRaw()
        {
            double x = _n < _malletLen ? Math.Sin(Math.PI * _n / _malletLen) : 0.0;
            double y = _m[0].Process(x) + _m[1].Process(x) + _m[2].Process(x) + _m[3].Process(x);
            // a 0.5 s raised fade before the end of life keeps the cut far below -100 dB
            if (_n > _lifeSamples - (int)(0.5 * Rate))
            {
                double u = (double)(_lifeSamples - _n) / (0.5 * Rate);
                y *= u <= 0.0 ? 0.0 : u * u;
            }
            if (++_n >= _lifeSamples) Active = false;
            return y;
        }
    }

    /// <summary>
    /// Short filtered noise burst with an optional sine thump: the door thud (30 Hz thump attack 5 ms / decay 0.5 s plus
    /// noise low-passed 400 Hz decaying 0.25 s) and the lift rail tick (800 Hz band noise, 25 ms).
    /// </summary>
    public sealed class BurstVoice : Voice
    {
        readonly Svf _f;
        readonly SvfMode _mode;
        readonly AdEnvelope _en = new AdEnvelope(), _et = new AdEnvelope();
        readonly SineOsc _osc = new SineOsc();
        readonly double _inc, _noiseMix, _thumpMix;
        Xorshift32 _rng;

        public BurstVoice(double rate, SvfMode mode, double fc, double q, double noiseAttack, double noiseDecay, double noiseMix,
                          double thumpHz, double thumpAttack, double thumpDecay, double thumpMix) : base(rate)
        {
            _mode = mode;
            _f = new Svf(fc, q, rate);
            _en.Set(noiseAttack, noiseDecay, rate);
            _et.Set(thumpAttack, thumpDecay, rate);
            _inc = thumpHz / rate;
            _noiseMix = noiseMix; _thumpMix = thumpMix;
        }

        public void Trigger(uint seed, double pan)
        {
            _rng = new Xorshift32(seed); _f.Reset(); _osc.Phase = 0.0;
            _en.Trigger();
            if (_thumpMix > 0.0) _et.Trigger(); else _et.Kill();
            SetPan(pan); Active = true;
        }

        public override double NextRaw()
        {
            double v = _f.Process(_rng.NextBipolar(), _mode) * _en.Next() * _noiseMix;
            if (_thumpMix > 0.0) v += _osc.Next(_inc) * _et.Next() * _thumpMix;
            if (!_en.Active && !_et.Active) Active = false;
            return v;
        }
    }

    /// <summary>
    /// Footstep (14.3 Footsteps). Concrete: noise low-passed 2.2 kHz, 70 ms, plus a 90 Hz thump, 120 ms. Steel plate: the
    /// same plus modes 420 / 1130 / 2370 Hz decaying 180 / 120 / 80 ms. Grating: 3-6 kHz tick, 40 ms, plus a 1.1 kHz ring,
    /// 90 ms (and a faint thump for body). Lift deck: hollow modes 140 and 600 Hz, 150 ms. Each component is calibrated
    /// to a unit peak, then the mix is calibrated to a unit peak per surface; pitch and level jitter are applied per step.
    /// </summary>
    public sealed class FootstepVoice : Voice
    {
        public const int SurfaceCount = 5;
        readonly Svf _nf = new Svf();
        readonly AdEnvelope _en = new AdEnvelope(), _et = new AdEnvelope();
        readonly SineOsc _osc = new SineOsc();
        readonly ModalResonator[] _m = { new ModalResonator(), new ModalResonator(), new ModalResonator() };
        Xorshift32 _rng;
        SvfMode _mode;
        int _modes;
        double _inc, _wn, _wt, _wm;
        int _n, _life;

        public FootstepVoice(double rate) : base(rate) { Send = 1f; }

        /// <summary>
        /// Configure and start a step. componentMask bit 0 = noise, 1 = thump, 2 = modes. With compNorm == null the raw
        /// components play at weight 1 (calibration); otherwise each component is scaled by its unit-peak normaliser
        /// compNorm[surface * 3 + component] and its mix weight.
        /// </summary>
        public void Trigger(SurfaceKind kind, double pitch, uint seed, double pan, int componentMask, double[] compNorm)
        {
            int s = (int)kind;
            double cn = compNorm != null ? compNorm[s * 3 + 0] : 1.0;
            double ct = compNorm != null ? compNorm[s * 3 + 1] : 1.0;
            double cm = compNorm != null ? compNorm[s * 3 + 2] : 1.0;
            double noiseT60, thumpHz = 90.0 * pitch, wn, wt, wm;
            _modes = 0;
            switch (kind)
            {
                case SurfaceKind.SteelPlate:
                    _mode = SvfMode.LowPass; _nf.Set(2200.0 * pitch, 0.7, Rate); noiseT60 = 0.070;
                    _m[0].Set(420.0 * pitch, 0.180, 1.0, Rate); _m[1].Set(1130.0 * pitch, 0.120, 0.7, Rate); _m[2].Set(2370.0 * pitch, 0.080, 0.5, Rate);
                    _modes = 3; wn = 0.8; wt = 0.7; wm = 0.6;
                    break;
                case SurfaceKind.Grating:
                    _mode = SvfMode.BandPassNormalized; _nf.Set(Math.Sqrt(3000.0 * 6000.0) * pitch, 1.41, Rate); noiseT60 = 0.040;
                    _m[0].Set(1100.0 * pitch, 0.090, 1.0, Rate);
                    _modes = 1; wn = 1.0; wt = 0.25; wm = 0.7;
                    break;
                case SurfaceKind.LiftDeck:
                    _mode = SvfMode.LowPass; _nf.Set(1200.0 * pitch, 0.7, Rate); noiseT60 = 0.050;
                    _m[0].Set(140.0 * pitch, 0.150, 1.0, Rate); _m[1].Set(600.0 * pitch, 0.150, 0.6, Rate);
                    _modes = 2; wn = 0.4; wt = 0.0; wm = 1.0;
                    break;
                default: // Concrete and Stair
                    _mode = SvfMode.LowPass; _nf.Set(2200.0 * pitch, 0.7, Rate); noiseT60 = 0.070;
                    wn = 1.0; wt = 0.9; wm = 0.0;
                    break;
            }
            _en.Set(0.001, noiseT60, Rate);
            _et.Set(0.002, 0.120, Rate);
            _inc = thumpHz / Rate;
            bool raw = compNorm == null;
            _wn = (componentMask & 1) != 0 && wn > 0.0 ? (raw ? 1.0 : wn * cn) : 0.0;
            _wt = (componentMask & 2) != 0 && wt > 0.0 ? (raw ? 1.0 : wt * ct) : 0.0;
            _wm = (componentMask & 4) != 0 && wm > 0.0 && _modes > 0 ? (raw ? 1.0 : wm * cm) : 0.0;
            _rng = new Xorshift32(seed);
            _nf.Reset(); _osc.Phase = 0.0;
            for (int i = 0; i < 3; i++) _m[i].Reset();
            _en.Trigger(); _et.Trigger();
            _n = 0; _life = (int)(0.35 * Rate);
            SetPan(pan); Active = true;
        }

        public override double NextRaw()
        {
            double w = _rng.NextBipolar();
            double ne = _en.Next();
            double v = 0.0;
            if (_wn != 0.0) v += _nf.Process(w, _mode) * ne * _wn;
            if (_wt != 0.0) v += _osc.Next(_inc) * _et.Next() * _wt;
            if (_wm != 0.0)
            {
                double x = w * ne;
                double m = _m[0].Process(x);
                if (_modes > 1) m += _m[1].Process(x);
                if (_modes > 2) m += _m[2].Process(x);
                v += m * _wm;
            }
            // fade the last 50 ms of the voice life so the cut is far below audibility
            if (_n > _life - (int)(0.05 * Rate))
            {
                double u = (double)(_life - _n) / (0.05 * Rate);
                v *= u <= 0.0 ? 0.0 : u * u;
            }
            if (++_n >= _life) Active = false;
            return v;
        }
    }
}
