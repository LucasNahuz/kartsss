using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Core;
using VortexKarts.Kart;

namespace VortexKarts.Race
{
    /// <summary>
    /// Orders karts by race progress a few times per second. Finished karts keep their finishing order.
    /// </summary>
    public class PositionManager : MonoBehaviour
    {
        private const float UpdateInterval = 0.15f;
        private readonly List<RaceProgressTracker> trackers = new List<RaceProgressTracker>();
        private float timer;

        public IReadOnlyList<RaceProgressTracker> Ordered => ordered;
        private readonly List<RaceProgressTracker> ordered = new List<RaceProgressTracker>();

        public void SetKarts(IList<KartController> karts)
        {
            trackers.Clear();
            for (int i = 0; i < karts.Count; i++)
            {
                var t = karts[i].GetComponent<RaceProgressTracker>();
                if (t != null) trackers.Add(t);
            }
            Recompute(true);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < UpdateInterval) return;
            timer = 0f;
            Recompute(false);
        }

        public void Recompute(bool silent)
        {
            ordered.Clear();
            ordered.AddRange(trackers);
            ordered.Sort(Compare);
            for (int i = 0; i < ordered.Count; i++)
            {
                int pos = i + 1;
                if (ordered[i].Position != pos)
                {
                    ordered[i].Position = pos;
                    if (!silent)
                    {
                        var kart = ordered[i].GetComponent<KartController>();
                        GameEvents.RaisePositionChanged(kart, pos);
                    }
                }
            }
        }

        private static int Compare(RaceProgressTracker a, RaceProgressTracker b)
        {
            if (a.Finished && b.Finished) return a.FinishTime.CompareTo(b.FinishTime);
            if (a.Finished) return -1;
            if (b.Finished) return 1;
            return b.TotalProgress.CompareTo(a.TotalProgress);
        }

        public RaceProgressTracker GetAt(int position)
        {
            int i = position - 1;
            return i >= 0 && i < ordered.Count ? ordered[i] : null;
        }
    }
}
