using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Kart;

namespace VortexKarts.Track
{
    /// <summary>
    /// Describes what a collider feels like under the wheels. Registered in a static map so the kart's
    /// ground raycast can look it up without GetComponent every physics step.
    /// </summary>
    public class TrackSurface : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Road;
        [Tooltip("Multiplier on top speed while driving on this surface.")]
        public float SpeedFactor = 1f;
        [Tooltip("Multiplier on lateral grip.")]
        public float GripFactor = 1f;

        private static readonly Dictionary<Collider, TrackSurface> registry = new Dictionary<Collider, TrackSurface>();
        private readonly List<Collider> registered = new List<Collider>();

        public static TrackSurface Get(Collider collider)
        {
            if (collider == null) return null;
            TrackSurface s;
            if (registry.TryGetValue(collider, out s)) return s;
            // Late registration for colliders added after Awake.
            s = collider.GetComponentInParent<TrackSurface>();
            registry[collider] = s;
            return s;
        }

        private void Awake()
        {
            Register();
        }

        /// <summary>Call after adding colliders at runtime.</summary>
        public void Register()
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                registry[colliders[i]] = this;
                if (!registered.Contains(colliders[i])) registered.Add(colliders[i]);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < registered.Count; i++)
            {
                if (registered[i] != null) registry.Remove(registered[i]);
            }
            registered.Clear();
        }

        public static TrackSurface Configure(GameObject go, SurfaceType type, float speedFactor = 1f, float gripFactor = 1f)
        {
            var s = go.GetComponent<TrackSurface>();
            if (s == null) s = go.AddComponent<TrackSurface>();
            s.Type = type;
            s.SpeedFactor = speedFactor;
            s.GripFactor = gripFactor;
            s.Register();
            return s;
        }
    }
}
