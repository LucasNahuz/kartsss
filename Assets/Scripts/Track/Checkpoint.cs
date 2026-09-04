using UnityEngine;
using VortexKarts.Race;

namespace VortexKarts.Track
{
    /// <summary>Invisible gate across the road. Index 0 is the start/finish line.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public int Index;
        public float Distance;
        public bool IsFinishLine => Index == 0;
        public Vector3 Forward = Vector3.forward;

        public static Checkpoint Create(Transform parent, int index, TrackNode node, float height = 10f, float depth = 3f)
        {
            var go = new GameObject("Checkpoint_" + index);
            go.transform.SetParent(parent, false);
            go.transform.position = node.Position + node.Up * (height * 0.5f - 1f);
            go.transform.rotation = Quaternion.LookRotation(node.Forward, node.Up);
            go.layer = 2; // Ignore Raycast
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(node.Width + 30f, height, depth);
            var cp = go.AddComponent<Checkpoint>();
            cp.Index = index;
            cp.Distance = node.Distance;
            cp.Forward = node.Forward;
            return cp;
        }

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var tracker = rb.GetComponent<RaceProgressTracker>();
            if (tracker != null) tracker.OnCheckpointTriggered(this);
        }
    }
}
