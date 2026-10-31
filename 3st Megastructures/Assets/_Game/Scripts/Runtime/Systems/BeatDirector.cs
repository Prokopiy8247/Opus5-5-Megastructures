// BeatDirector: current beat with 12 m hysteresis; logs each first entry in order (the autotest asserts
// beats 1..7 fire exactly once and in order) and the beacon trigger.
using System;
using UnityEngine;

namespace Megastructure
{
    [DefaultExecutionOrder(600)]
    public class BeatDirector : MonoBehaviour
    {
        public static BeatDirector Instance { get; private set; }
        public int Current { get; private set; } = -1;
        public int EnteredCount { get; private set; }
        public bool OrderOk { get; private set; } = true;
        public bool GoalFired { get; private set; }
        public int GoalCount { get; private set; }
        public static event Action<int, double> BeatEntered;
        readonly bool[] m_entered = new bool[ThreadConstants.BeatCount];

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void LateUpdate()
        {
            if (!GameBootstrap.Ready) return;
            double s = ProgressDirector.S;
            int b = Current < 0 ? ThreadMath.BeatIndex(s) : Current;
            var bounds = ThreadConstants.BeatBoundaries;
            while (b < ThreadConstants.BeatCount - 1 && s >= bounds[b + 1]) b++;
            while (b > 0 && s < bounds[b] - ThreadConstants.Hysteresis) b--;
            if (b != Current)
            {
                Current = b;
                if (!m_entered[b])
                {
                    m_entered[b] = true;
                    if (b != EnteredCount && !(EnteredCount == 0 && CliArgs.StartS > 0)) OrderOk = false;
                    EnteredCount++;
                    Debug.Log($"[THREAD] beat {b + 1} ({ThreadConstants.BeatNames[b]}) entered at s={s:F2} t={Time.timeSinceLevelLoadAsDouble:F2}");
                    BeatEntered?.Invoke(b, s);
                }
            }
            var p = ThreadServices.Player;
            if (p != null && p.Finished && !GoalFired)
            {
                GoalFired = true;
                GoalCount++;
                Debug.Log($"[THREAD] goal trigger at s={s:F2} t={Time.timeSinceLevelLoadAsDouble:F2}");
            }
        }
    }
}
