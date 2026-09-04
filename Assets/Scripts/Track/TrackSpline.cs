using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Track
{
    public struct SplineSample
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public float Width;
        public int Segment;
        public float T;
        public float Distance;
    }

    /// <summary>
    /// Catmull-Rom spline through control points with interpolated widths. Closed for the main circuit,
    /// open for shortcuts. Provides uniform-distance sampling used by every track system.
    /// </summary>
    public class TrackSpline
    {
        private readonly Vector3[] points;
        private readonly float[] widths;
        private readonly bool closed;

        public int PointCount => points.Length;
        public int SegmentCount => closed ? points.Length : points.Length - 1;
        public bool Closed => closed;

        public TrackSpline(IList<Vector3> controlPoints, IList<float> controlWidths, bool closed)
        {
            points = new Vector3[controlPoints.Count];
            widths = new float[controlPoints.Count];
            for (int i = 0; i < controlPoints.Count; i++)
            {
                points[i] = controlPoints[i];
                widths[i] = controlWidths != null && i < controlWidths.Count ? controlWidths[i] : 12f;
            }
            this.closed = closed;
        }

        private Vector3 P(int i)
        {
            int n = points.Length;
            if (closed)
            {
                i %= n;
                if (i < 0) i += n;
                return points[i];
            }
            return points[Mathf.Clamp(i, 0, n - 1)];
        }

        private float W(int i)
        {
            int n = widths.Length;
            if (closed)
            {
                i %= n;
                if (i < 0) i += n;
                return widths[i];
            }
            return widths[Mathf.Clamp(i, 0, n - 1)];
        }

        public Vector3 Evaluate(int segment, float t)
        {
            Vector3 p0 = P(segment - 1), p1 = P(segment), p2 = P(segment + 1), p3 = P(segment + 2);
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public Vector3 Tangent(int segment, float t)
        {
            Vector3 p0 = P(segment - 1), p1 = P(segment), p2 = P(segment + 1), p3 = P(segment + 2);
            float t2 = t * t;
            Vector3 d = 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
            if (d.sqrMagnitude < 1e-6f) d = p2 - p1;
            return d.normalized;
        }

        public float WidthAt(int segment, float t)
        {
            float s = t * t * (3f - 2f * t);
            return Mathf.Lerp(W(segment), W(segment + 1), s);
        }

        public float EstimateLength(int subdivisionsPerSegment = 24)
        {
            float length = 0f;
            for (int s = 0; s < SegmentCount; s++)
            {
                Vector3 prev = Evaluate(s, 0f);
                for (int i = 1; i <= subdivisionsPerSegment; i++)
                {
                    Vector3 cur = Evaluate(s, i / (float)subdivisionsPerSegment);
                    length += Vector3.Distance(prev, cur);
                    prev = cur;
                }
            }
            return length;
        }

        /// <summary>
        /// Samples the spline at (approximately) uniform arc-length spacing. Closed splines get a spacing
        /// adjusted so the loop divides evenly and the last sample joins the first.
        /// </summary>
        public List<SplineSample> SampleByDistance(float spacing, int subdivisionsPerSegment = 40)
        {
            var result = new List<SplineSample>();
            float total = EstimateLength(subdivisionsPerSegment);
            if (total <= 0.01f) return result;
            int count = Mathf.Max(4, Mathf.RoundToInt(total / spacing));
            float step = total / count;
            if (!closed) count += 1; // include the end point

            float travelled = 0f;
            float nextAt = 0f;
            Vector3 prev = Evaluate(0, 0f);
            int prevSeg = 0;
            float prevT = 0f;

            // First sample.
            result.Add(MakeSample(0, 0f, 0f));
            nextAt = step;

            for (int s = 0; s < SegmentCount && result.Count < count; s++)
            {
                for (int i = 1; i <= subdivisionsPerSegment && result.Count < count; i++)
                {
                    float t = i / (float)subdivisionsPerSegment;
                    Vector3 cur = Evaluate(s, t);
                    float d = Vector3.Distance(prev, cur);
                    while (travelled + d >= nextAt && result.Count < count)
                    {
                        float f = d > 1e-5f ? (nextAt - travelled) / d : 1f;
                        float tt = Mathf.Lerp(prevT, t, f);
                        int seg = s;
                        // prevT belongs to the previous segment when i == 1
                        if (i == 1 && prevSeg != s)
                        {
                            // interpolate across the boundary: prevT was 1.0 of previous segment == 0 of this one
                            tt = Mathf.Lerp(0f, t, f);
                        }
                        result.Add(MakeSample(seg, tt, nextAt));
                        nextAt += step;
                    }
                    travelled += d;
                    prev = cur;
                    prevSeg = s;
                    prevT = t;
                }
                prevT = 0f;
            }

            if (!closed && result.Count < count)
            {
                result.Add(MakeSample(SegmentCount - 1, 1f, total));
            }
            return result;
        }

        private SplineSample MakeSample(int segment, float t, float distance)
        {
            return new SplineSample
            {
                Position = Evaluate(segment, t),
                Tangent = Tangent(segment, t),
                Width = WidthAt(segment, t),
                Segment = segment,
                T = t,
                Distance = distance
            };
        }
    }
}
