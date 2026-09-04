using UnityEngine;
using VortexKarts.Data;
using VortexKarts.Kart;
using VortexKarts.PowerUps;
using VortexKarts.Race;
using VortexKarts.Track;

namespace VortexKarts.Debugging
{
    /// <summary>
    /// Development-only overlay (editor / development builds). Rendered with IMGUI on the mirror window,
    /// not inside the headset: it is a tooling aid, not a game screen.
    /// Toggle with F1. Lets you change lap, jump to a position, give power-ups, reset the kart, toggle AI,
    /// change time scale and draw waypoints / racing line / checkpoints.
    /// </summary>
    public class DebugRacePanel : MonoBehaviour
    {
        private RaceManager race;
        private bool visible;
        private bool drawNodes, drawLine, drawCheckpoints;
        private int powerUpIndex;
        private Rect window = new Rect(10f, 10f, 330f, 520f);

        public static DebugRacePanel Create(RaceManager raceManager)
        {
            var go = new GameObject("DebugRacePanel");
            var p = go.AddComponent<DebugRacePanel>();
            p.race = raceManager;
            return p;
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) visible = !visible;
            if (drawNodes || drawLine || drawCheckpoints) DrawTrackDebug();
        }

        private void DrawTrackDebug()
        {
            var track = TrackRuntime.Instance;
            if (track == null) return;
            var nodes = track.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var a = nodes[i];
                var b = nodes[(i + 1) % nodes.Count];
                if (drawLine)
                {
                    float speedT = Mathf.InverseLerp(10f, 36f, a.RecommendedSpeed);
                    Debug.DrawLine(a.Position + Vector3.up * 0.3f, b.Position + Vector3.up * 0.3f, Color.Lerp(Color.red, Color.green, speedT));
                }
                if (drawNodes && i % 2 == 0)
                {
                    Debug.DrawLine(a.LeftEdge + Vector3.up * 0.2f, a.RightEdge + Vector3.up * 0.2f, a.IsGap ? Color.magenta : Color.cyan);
                }
            }
            for (int s = 0; s < track.ShortcutNodes.Count && drawLine; s++)
            {
                var list = track.ShortcutNodes[s];
                for (int i = 0; i < list.Count - 1; i++) Debug.DrawLine(list[i].Position + Vector3.up * 0.3f, list[i + 1].Position + Vector3.up * 0.3f, Color.yellow);
            }
            if (drawCheckpoints)
            {
                for (int i = 0; i < track.Checkpoints.Count; i++)
                {
                    var cp = track.Checkpoints[i];
                    var box = cp.GetComponent<BoxCollider>();
                    if (box == null) continue;
                    Vector3 c = cp.transform.position;
                    Vector3 r = cp.transform.right * box.size.x * 0.5f;
                    Vector3 u = cp.transform.up * box.size.y * 0.5f;
                    Color col = cp.IsFinishLine ? Color.white : Color.green;
                    Debug.DrawLine(c - r - u, c + r - u, col);
                    Debug.DrawLine(c + r - u, c + r + u, col);
                    Debug.DrawLine(c + r + u, c - r + u, col);
                    Debug.DrawLine(c - r + u, c - r - u, col);
                }
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                GUI.Label(new Rect(10f, 10f, 300f, 22f), "F1: debug panel");
                return;
            }
            window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Debug Race Panel");
        }

        private void DrawWindow(int id)
        {
            if (race == null) return;
            var player = race.PlayerKart;
            var tracker = player != null ? player.GetComponent<RaceProgressTracker>() : null;

            GUILayout.Label("State: " + race.State + "   Time: " + race.RaceTime.ToString("0.0"));
            if (tracker != null)
            {
                GUILayout.Label("Pos " + tracker.Position + "  Lap " + tracker.Lap + "  CP next " + tracker.NextCheckpoint +
                                "  Dist " + tracker.DistanceAlong.ToString("0") + (tracker.InShortcut ? "  [shortcut]" : ""));
                GUILayout.Label("Speed " + player.ForwardSpeed.ToString("0.0") + " m/s  Surface " + player.Surface +
                                "  Grounded " + player.IsGrounded + "  Drift L" + player.Drift.Level);
            }

            GUILayout.Space(6f);
            GUILayout.Label("Lap");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-1") && tracker != null) race.DebugSetPlayerLap(tracker.Lap - 1);
            if (GUILayout.Button("+1") && tracker != null) race.DebugSetPlayerLap(tracker.Lap + 1);
            if (GUILayout.Button("Last lap") && tracker != null) race.DebugSetPlayerLap(race.TotalLaps - 1);
            GUILayout.EndHorizontal();

            GUILayout.Label("Teleport to position");
            GUILayout.BeginHorizontal();
            for (int p = 1; p <= Mathf.Min(8, race.Karts.Count); p++)
            {
                if (GUILayout.Button(p.ToString())) race.DebugTeleportPlayerToPosition(p);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            var powerUps = GameDatabase.PowerUps;
            if (powerUps.Count > 0)
            {
                powerUpIndex = Mathf.Clamp(powerUpIndex, 0, powerUps.Count - 1);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(30f))) powerUpIndex = (powerUpIndex - 1 + powerUps.Count) % powerUps.Count;
                GUILayout.Label(powerUps[powerUpIndex].displayName, GUILayout.Width(170f));
                if (GUILayout.Button(">", GUILayout.Width(30f))) powerUpIndex = (powerUpIndex + 1) % powerUps.Count;
                if (GUILayout.Button("Give") && player != null)
                {
                    var inv = player.GetComponent<PowerUpInventory>();
                    if (inv != null) inv.Give(powerUps[powerUpIndex]);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset kart") && player != null) player.Respawn.RequestRespawn("debug");
            if (GUILayout.Button(race.AIEnabled ? "AI: ON" : "AI: OFF")) race.DebugSetAIEnabled(!race.AIEnabled);
            GUILayout.EndHorizontal();

            GUILayout.Label("Time scale");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.25x")) Time.timeScale = 0.25f;
            if (GUILayout.Button("0.5x")) Time.timeScale = 0.5f;
            if (GUILayout.Button("1x")) Time.timeScale = 1f;
            if (GUILayout.Button("2x")) Time.timeScale = 2f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            drawNodes = GUILayout.Toggle(drawNodes, "Draw waypoints (cross sections)");
            drawLine = GUILayout.Toggle(drawLine, "Draw racing line (speed colour)");
            drawCheckpoints = GUILayout.Toggle(drawCheckpoints, "Draw checkpoints");
            GUILayout.Label("Gizmos must be enabled in the Game view to see lines.");

            GUILayout.Space(6f);
            if (GUILayout.Button("Make everyone finish (CPU)"))
            {
                for (int i = 0; i < race.Karts.Count; i++)
                {
                    var k = race.Karts[i];
                    if (k == player) continue;
                    var t = k.GetComponent<RaceProgressTracker>();
                    if (t != null) t.DebugSetLap(race.TotalLaps - 1);
                }
            }
            GUI.DragWindow();
        }
    }
}
