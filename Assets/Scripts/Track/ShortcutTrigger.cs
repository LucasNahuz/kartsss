using UnityEngine;
using VortexKarts.Race;

namespace VortexKarts.Track
{
    /// <summary>
    /// Gate at a shortcut entry or exit. Entering tells the progress tracker to follow the shortcut path
    /// and allows skipping the main-road checkpoints between entry and exit.
    /// </summary>
    public class ShortcutTrigger : MonoBehaviour
    {
        public int ShortcutIndex;
        public bool IsExit;
        public int ExitCheckpointIndex;

        public static ShortcutTrigger Create(Transform parent, TrackNode node, int shortcutIndex, bool isExit, int exitCheckpoint, float width)
        {
            var go = new GameObject((isExit ? "ShortcutExit_" : "ShortcutEntry_") + shortcutIndex);
            go.transform.SetParent(parent, false);
            go.transform.position = node.Position + node.Up * 3f;
            go.transform.rotation = Quaternion.LookRotation(node.Forward, node.Up);
            go.layer = 2;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width + 4f, 8f, 3f);
            var t = go.AddComponent<ShortcutTrigger>();
            t.ShortcutIndex = shortcutIndex;
            t.IsExit = isExit;
            t.ExitCheckpointIndex = exitCheckpoint;
            return t;
        }

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var tracker = rb.GetComponent<RaceProgressTracker>();
            if (tracker == null) return;
            if (IsExit) tracker.ExitShortcut(ShortcutIndex);
            else tracker.EnterShortcut(ShortcutIndex, ExitCheckpointIndex);
        }
    }
}
