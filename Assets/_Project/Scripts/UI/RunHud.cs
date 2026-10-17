using UnityEngine;

namespace Survivors
{
    // Prototype HUD drawn with IMGUI (no uGUI dependency). Replaced by real UI later.
    public sealed class RunHud : MonoBehaviour
    {
        GUIStyle _label, _title, _card;
        Texture2D _circle;

        void Awake()
        {
            _circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                    _circle.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 31f ? 1f : 0f));
                }
            _circle.Apply();
        }

        void OnDestroy()
        {
            if (_circle != null) Destroy(_circle);
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
            _title = new GUIStyle(_label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _card = new GUIStyle(GUI.skin.button) { fontSize = 24, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(20, 20, 10, 10) };
        }

        void OnGUI()
        {
            var run = RunController.I;
            var hero = Hero.I;
            if (run == null || hero == null) return;
            EnsureStyles();

            float s = Mathf.Max(0.6f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float W = Screen.width / s, H = Screen.height / s;

            DrawJoystick(s);
            DrawTopBar(run, hero, W);

            if (run.Phase == RunPhase.LevelUp) DrawLevelUp(run, W, H);
            else if (run.Phase == RunPhase.Cleared) DrawResult("스테이지 클리어!", run, W, H);
            else if (run.Phase == RunPhase.Failed) DrawResult("패배", run, W, H);
        }

        void DrawJoystick(float s)
        {
            if (!InputService.Dragging) return;
            Vector2 o = InputService.Origin, c = InputService.Current;
            float ox = o.x / s, oy = (Screen.height - o.y) / s;
            float cx = c.x / s, cy = (Screen.height - c.y) / s;
            float R = InputService.Radius / s;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(new Rect(ox - R, oy - R, R * 2f, R * 2f), _circle);
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(new Rect(cx - 24f, cy - 24f, 48f, 48f), _circle);
            GUI.color = prev;
        }

        void DrawTopBar(RunController run, Hero hero, float W)
        {
            float need = ExpCurve.Required(run.Level);
            Fill(new Rect(0, 0, W, 16), new Color(0, 0, 0, 0.6f));
            Fill(new Rect(0, 0, W * Mathf.Clamp01(run.Exp / need), 16), new Color(0.35f, 0.7f, 1f));

            Fill(new Rect(12, 26, 260, 18), new Color(0, 0, 0, 0.6f));
            Fill(new Rect(12, 26, 260 * Mathf.Clamp01(hero.Hp / hero.Stats.MaxHp), 18), new Color(0.85f, 0.25f, 0.25f));

            int left = Mathf.Max(0, Mathf.CeilToInt(Config.StageDuration - run.Elapsed));
            GUI.Label(new Rect(12, 48, 300, 30), "Lv." + run.Level + "   처치 " + run.Kills, _label);
            GUI.Label(new Rect(W - 130, 22, 120, 30), (left / 60) + ":" + (left % 60).ToString("00"), _label);
        }

        void DrawLevelUp(RunController run, float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.65f));
            GUI.Label(new Rect(0, H * 0.12f, W, 50), "레벨 업!  스킬을 선택하세요", _title);

            float cw = Mathf.Min(W - 40f, 560f), ch = 120f, gap = 16f;
            float x = (W - cw) / 2f, y = H * 0.25f;
            var choices = run.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                var def = choices[i];
                int cur = SkillLibrary.LevelOf(run.Skills, def);
                int next = Mathf.Min(def.MaxLevel, cur + 1);
                string kind = def == SkillLibrary.Heal ? "" : (def.Active ? "[액티브] " : "[패시브] ");
                string head = def.Name + "  " + (cur == 0 ? "NEW" : "Lv." + cur + " → Lv." + next);
                string text = kind + head + "\n" + def.LevelText[next - 1];
                if (GUI.Button(new Rect(x, y + i * (ch + gap), cw, ch), text, _card))
                {
                    run.Pick(def);
                    break;
                }
            }
        }

        void DrawResult(string title, RunController run, float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.7f));
            GUI.Label(new Rect(0, H * 0.25f, W, 60), title, _title);
            var center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, H * 0.25f + 70, W, 80), "Lv." + run.Level + "   처치 " + run.Kills + "\n생존 " + Mathf.FloorToInt(run.Elapsed) + "초", center);
            if (GUI.Button(new Rect(W / 2f - 130f, H * 0.25f + 180f, 260f, 70f), "다시 하기", _card))
                Bootstrap.RequestRestart();
        }
    }
}
