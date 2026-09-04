using System;
using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Utils
{
    /// <summary>
    /// Components implementing this get notified when their pooled object is spawned or returned.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>Marker component attached to every pooled object so it can be returned by reference.</summary>
    public class PooledObject : MonoBehaviour
    {
        public string PoolKey;
        public bool IsActiveInPool;
    }

    /// <summary>
    /// Scene-scoped pool manager. Pools are keyed by string and use a factory delegate instead of prefabs,
    /// because every gameplay object in this project is built procedurally.
    /// Projectiles, mines, oil slicks, VFX and pickup boxes all go through here so the race never
    /// calls Instantiate/Destroy in the hot path.
    /// </summary>
    public class ObjectPoolManager : MonoBehaviour
    {
        private static ObjectPoolManager instance;

        public static ObjectPoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("ObjectPoolManager");
                    instance = go.AddComponent<ObjectPoolManager>();
                }
                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        private class Pool
        {
            public Func<GameObject> Factory;
            public readonly Stack<GameObject> Inactive = new Stack<GameObject>();
            public readonly List<GameObject> Active = new List<GameObject>();
            public Transform Root;
        }

        private readonly Dictionary<string, Pool> pools = new Dictionary<string, Pool>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Registers (or replaces) a pool and optionally pre-warms it.</summary>
        public void RegisterPool(string key, Func<GameObject> factory, int prewarm = 0)
        {
            if (string.IsNullOrEmpty(key) || factory == null) return;
            Pool pool;
            if (!pools.TryGetValue(key, out pool))
            {
                pool = new Pool();
                var root = new GameObject("Pool_" + key);
                root.transform.SetParent(transform, false);
                pool.Root = root.transform;
                pools[key] = pool;
            }
            pool.Factory = factory;
            for (int i = 0; i < prewarm; i++)
            {
                var go = CreateNew(key, pool);
                if (go == null) break;
                go.SetActive(false);
                pool.Inactive.Push(go);
            }
        }

        public bool HasPool(string key) => pools.ContainsKey(key);

        private GameObject CreateNew(string key, Pool pool)
        {
            GameObject go = null;
            try
            {
                go = pool.Factory();
            }
            catch (Exception e)
            {
                Debug.LogError("[ObjectPool] Factory for '" + key + "' threw: " + e);
            }
            if (go == null) return null;
            var marker = go.GetComponent<PooledObject>();
            if (marker == null) marker = go.AddComponent<PooledObject>();
            marker.PoolKey = key;
            go.transform.SetParent(pool.Root, false);
            return go;
        }

        public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
        {
            Pool pool;
            if (!pools.TryGetValue(key, out pool))
            {
                Debug.LogWarning("[ObjectPool] No pool registered for '" + key + "'.");
                return null;
            }

            GameObject go = null;
            while (pool.Inactive.Count > 0 && go == null)
            {
                go = pool.Inactive.Pop();
            }
            if (go == null) go = CreateNew(key, pool);
            if (go == null) return null;

            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            var marker = go.GetComponent<PooledObject>();
            if (marker != null) marker.IsActiveInPool = true;
            pool.Active.Add(go);

            var poolables = go.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawned();
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (go == null) return;
            var marker = go.GetComponent<PooledObject>();
            if (marker == null || string.IsNullOrEmpty(marker.PoolKey))
            {
                Destroy(go);
                return;
            }
            if (!marker.IsActiveInPool) return;
            marker.IsActiveInPool = false;

            var poolables = go.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnDespawned();

            Pool pool;
            if (pools.TryGetValue(marker.PoolKey, out pool))
            {
                pool.Active.Remove(go);
                go.SetActive(false);
                go.transform.SetParent(pool.Root, false);
                pool.Inactive.Push(go);
            }
            else
            {
                Destroy(go);
            }
        }

        public void DespawnAll(string key)
        {
            Pool pool;
            if (!pools.TryGetValue(key, out pool)) return;
            var copy = pool.Active.ToArray();
            for (int i = 0; i < copy.Length; i++) Despawn(copy[i]);
        }

        public void DespawnEverything()
        {
            foreach (var kv in pools)
            {
                var copy = kv.Value.Active.ToArray();
                for (int i = 0; i < copy.Length; i++) Despawn(copy[i]);
            }
        }

        public int CountActive(string key)
        {
            Pool pool;
            return pools.TryGetValue(key, out pool) ? pool.Active.Count : 0;
        }
    }
}
