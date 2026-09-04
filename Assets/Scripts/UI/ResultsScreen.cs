using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VortexKarts.Core;
using VortexKarts.Race;
using VortexKarts.Utils;
using VortexKarts.VR;

namespace VortexKarts.UI
{
    /// <summary>
    /// End-of-race board: standings with total time and best lap, player row highlighted, record notice,
    /// and CONTINUAR / REPETIR / MENÚ PRINCIPAL.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        private RaceManager race;
        private Canvas canvas;

        public static ResultsScreen Create(RaceManager raceManager)
        {
            var go = new GameObject("ResultsScreen");
            var rs = go.AddComponent<ResultsScreen>();
            rs.race = raceManager;
            rs.race.OnResultsReady += rs.Show;
            return rs;
        }

        private void OnDestroy()
        {
            if (race != null) race.OnResultsReady -= Show;
            UIFactory.SetCancelHandler(null);
        }

        private void Show(RaceResults results)
        {
            UIFactory.EnsureEventSystem();
            if (InputManager.Instance != null) InputManager.Instance.SetKartInputEnabled(false);
            Transform parent = VRManager.Instance != null ? VRManager.Instance.Rig : null;
            canvas = UIFactory.CreateWorldCanvas("ResultsCanvas", parent, new Vector3(0f, -0.05f, 2.3f), Quaternion.identity, new Vector2(1600f, 1000f), 0.0012f);
            var t = canvas.transform;
            UIFactory.Panel(t, Vector2.zero, new Vector2(1500f, 960f), UIFactory.Bg);

            var player = results.Player;
            string title = player != null ? MathUtil.Ordinal(player.Position) + " PUESTO" : "RESULTADOS";
            Color titleColor = player != null && player.Position <= 3 ? UIFactory.Good : UIFactory.Accent;
            UIFactory.Label(t, title, new Vector2(0f, 420f), new Vector2(1400f, 90f), 72, titleColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            string sub = race.TrackData != null ? race.TrackData.displayName : "";
            if (player != null) sub += "   ·   Tiempo " + MathUtil.FormatTime(player.TotalTime) + "   ·   Mejor vuelta " + MathUtil.FormatTime(player.BestLap);
            UIFactory.Label(t, sub, new Vector2(0f, 355f), new Vector2(1400f, 44f), 28, UIFactory.TextDim);
            if (results.NewRecord) UIFactory.Label(t, "¡NUEVO RÉCORD!", new Vector2(0f, 310f), new Vector2(600f, 40f), 30, UIFactory.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);

            // Header.
            float y = 255f;
            UIFactory.Label(t, "#", new Vector2(-620f, y), new Vector2(80f, 40f), 26, UIFactory.TextDim);
            UIFactory.Label(t, "PILOTO", new Vector2(-380f, y), new Vector2(380f, 40f), 26, UIFactory.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Label(t, "TIEMPO", new Vector2(150f, y), new Vector2(300f, 40f), 26, UIFactory.TextDim);
            UIFactory.Label(t, "MEJOR VUELTA", new Vector2(500f, y), new Vector2(320f, 40f), 26, UIFactory.TextDim);
            y -= 52f;
            for (int i = 0; i < results.Entries.Count; i++)
            {
                var e = results.Entries[i];
                Color c = e.IsPlayer ? UIFactory.Accent : UIFactory.TextColor;
                if (e.IsPlayer) UIFactory.Panel(t, new Vector2(0f, y), new Vector2(1420f, 50f), new Color(0.1f, 0.4f, 0.6f, 0.35f));
                UIFactory.Label(t, e.Position.ToString(), new Vector2(-620f, y), new Vector2(80f, 46f), 34, c, TextAnchor.MiddleCenter, FontStyle.Bold);
                string name = e.Kart != null ? e.Kart.DisplayName : "?";
                if (e.IsPlayer) name += "  (vos)";
                UIFactory.Label(t, name, new Vector2(-380f, y), new Vector2(380f, 46f), 32, c, TextAnchor.MiddleLeft);
                UIFactory.Label(t, e.Finished ? MathUtil.FormatTime(e.TotalTime) : "+" + MathUtil.FormatTime(Mathf.Max(0f, e.TotalTime - race.RaceTime)) + " est.", new Vector2(150f, y), new Vector2(300f, 46f), 30, c);
                UIFactory.Label(t, MathUtil.FormatTime(e.BestLap), new Vector2(500f, y), new Vector2(320f, 46f), 30, c);
                y -= 52f;
            }

            var cont = UIFactory.Button(t, "CONTINUAR", new Vector2(-460f, -400f), new Vector2(400f, 84f), () => race.ExitToMenu(), 36);
            var again = UIFactory.Button(t, "REPETIR", new Vector2(0f, -400f), new Vector2(400f, 84f), () => race.Restart(), 36);
            var menu = UIFactory.Button(t, "MENÚ PRINCIPAL", new Vector2(460f, -400f), new Vector2(400f, 84f), () => race.ExitToMenu(), 36);
            var list = new List<Selectable> { cont, again, menu };
            for (int i = 0; i < list.Count; i++)
            {
                var nav = list[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = list[(i - 1 + list.Count) % list.Count];
                nav.selectOnRight = list[(i + 1) % list.Count];
                list[i].navigation = nav;
            }
            UIFactory.SetCancelHandler(null);
            UIFactory.Select(cont);
        }
    }
}
