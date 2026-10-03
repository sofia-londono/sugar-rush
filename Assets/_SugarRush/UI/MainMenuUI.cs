using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SugarRush
{
    /// <summary>
    /// Main menu: Play, Choose racer, Options, Quit. Built in code on a UIDocument with the
    /// candy elements; pages swap with an animated transition.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        enum Page { Main, Characters, Options }

        public UIDocument document;
        public KartRoster roster;
        public KartShowcase showcase;

        VisualElement root;
        Page page;
        int previewKart;
        bool leaving;

        // Character page widgets that change when cycling karts
        CandyTitle charName;
        VisualElement speedFill, accelFill, handlingFill;

        // Start (not OnEnable) so the KartShowcase has spawned its karts in Awake first.
        void Start()
        {
            root = document.rootVisualElement;
            root.Clear();
            root.Add(new SprinkleRain(40));
            previewKart = GameSettings.SelectedKart;
            ShowPage(Page.Main);
            UIKit.FadeIn(root);
        }

        void Update()
        {
            if (leaving) return;
            if (page != Page.Main && UIKit.BackPressed())
            {
                if (page == Page.Characters) previewKart = GameSettings.SelectedKart;
                ShowPage(Page.Main);
                return;
            }

            if (page == Page.Characters)
            {
                int dir = UIKit.HorizontalPressed();
                if (dir != 0) CycleKart(dir);
            }
        }

        void ShowPage(Page next)
        {
            page = next;
            var screen = page switch
            {
                Page.Characters => BuildCharacters(),
                Page.Options => BuildOptions(),
                _ => BuildMain(),
            };
            UIKit.ShowScreen(root, screen);
            showcase.Show(page == Page.Characters ? previewKart : GameSettings.SelectedKart);
        }

        // ------------------------------------------------------------ Main

        VisualElement BuildMain()
        {
            var screen = UIKit.Div("screen", "screen--left");
            var column = UIKit.Div("menu-column");
            screen.Add(column);

            column.Add(UIKit.Title("Sugar Rush", CandyTone.Rainbow, "candy-title--xl"));
            column.Add(UIKit.Label(Loc.T("menu.subtitle"), "subtitle"));

            var play = UIKit.Button(Loc.T("menu.play"), Play);
            column.Add(play);
            column.Add(UIKit.Button(Loc.T("menu.characters"), () => ShowPage(Page.Characters), "candy-button--mint"));
            column.Add(UIKit.Button(Loc.T("menu.options"), () => ShowPage(Page.Options), "candy-button--lavender"));
#if !UNITY_WEBGL
            column.Add(UIKit.Button(Loc.T("menu.quit"), Quit, "candy-button--lemon"));
#endif

            float best = GameSettings.GetBestTime(GameSettings.Laps);
            if (best > 0f)
                column.Add(UIKit.Label(Loc.T("menu.best", GameSettings.Laps, Loc.Time(best)), "small-text"));

            screen.Add(UIKit.Label(Loc.T("menu.credits"), "small-text", "footer"));
            UIKit.FocusLater(play);
            return screen;
        }

        void Play()
        {
            if (leaving) return;
            leaving = true;
            GameSettings.Save();
            UIKit.FadeOut(root, () => SceneManager.LoadScene(SceneNames.Race));
        }

        void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ------------------------------------------------------------ Characters

        VisualElement BuildCharacters()
        {
            var screen = UIKit.Div("screen", "screen--left");
            var column = UIKit.Div("menu-column");
            screen.Add(column);
            column.Add(UIKit.Title(Loc.T("char.title"), CandyTone.Rainbow, "candy-title--lg"));

            var nameRow = UIKit.Div("row", "char-name-row");
            nameRow.Add(UIKit.ArrowButton("‹", () => CycleKart(-1)));
            charName = UIKit.Title("", CandyTone.Pink, "candy-title--md");
            charName.AddToClassList("char-name");
            nameRow.Add(charName);
            nameRow.Add(UIKit.ArrowButton("›", () => CycleKart(1)));
            column.Add(nameRow);

            var stats = new FrostingPanel(CandyTone.Mint, 7);
            stats.Add(StatRow("char.speed", "", out speedFill));
            stats.Add(StatRow("char.accel", "stat-bar__fill--mint", out accelFill));
            stats.Add(StatRow("char.handling", "stat-bar__fill--lavender", out handlingFill));

            var buttons = UIKit.Div("row");
            buttons.Add(UIKit.Button(Loc.T("menu.back"), () => { previewKart = GameSettings.SelectedKart; ShowPage(Page.Main); },
                "candy-button--lemon", "candy-button--small"));
            var pick = UIKit.Button(Loc.T("menu.select"), PickKart, "candy-button--small");
            buttons.Add(pick);
            stats.Add(buttons);
            column.Add(stats);

            RefreshCharacter();
            UIKit.FocusLater(pick);
            return screen;
        }

        VisualElement StatRow(string key, string fillClass, out VisualElement fill)
        {
            var row = UIKit.Div("stat-row");
            row.Add(UIKit.Label(Loc.T(key), "stat-row__label"));
            var bar = UIKit.Div("stat-bar");
            fill = UIKit.Div("stat-bar__fill");
            if (!string.IsNullOrEmpty(fillClass)) fill.AddToClassList(fillClass);
            var stripes = new CandyStripes { stripeWidth = 10f };
            stripes.AddToClassList("stat-bar__stripes");
            fill.Add(stripes);
            bar.Add(fill);
            row.Add(bar);
            return row;
        }

        void CycleKart(int dir)
        {
            int n = roster.karts.Length;
            previewKart = ((previewKart + dir) % n + n) % n;
            showcase.Show(previewKart);
            RefreshCharacter();
            UIKit.Pop(charName);
        }

        void RefreshCharacter()
        {
            var entry = roster.Get(previewKart);
            charName.Text = entry.displayName;
            // Stat values are data, so they are the one place the UI sets sizes from code.
            speedFill.style.width = Length.Percent(Mathf.Lerp(15f, 100f, entry.speed));
            accelFill.style.width = Length.Percent(Mathf.Lerp(15f, 100f, entry.acceleration));
            handlingFill.style.width = Length.Percent(Mathf.Lerp(15f, 100f, entry.handling));
        }

        void PickKart()
        {
            GameSettings.SelectedKart = previewKart;
            GameSettings.Save();
            ShowPage(Page.Main);
        }

        // ------------------------------------------------------------ Options

        VisualElement BuildOptions()
        {
            var screen = UIKit.Div("screen");
            var panel = new FrostingPanel(CandyTone.Lavender, 11);
            panel.AddToClassList("frosting-panel--wide");
            var heading = UIKit.Title(Loc.T("opt.title"), CandyTone.Pink, "candy-title--md");
            heading.AddToClassList("panel-heading");
            panel.Add(heading);

            panel.Add(OptionRow("opt.language", () => Loc.T("opt.language.value"), dir =>
            {
                GameSettings.Language = GameSettings.Language == Language.Spanish ? Language.English : Language.Spanish;
                GameSettings.Save();
                ShowPage(Page.Options); // rebuild every label in the new language
            }));

            panel.Add(OptionRow("opt.volume", () => Mathf.RoundToInt(GameSettings.Volume * 100f) + "%", dir =>
            {
                GameSettings.Volume = Mathf.Clamp01(Mathf.Round(GameSettings.Volume * 10f + dir) / 10f);
                GameSettings.Save();
            }));

            panel.Add(OptionRow("opt.quality", () => Loc.T("opt.quality." + Mathf.Clamp(GameSettings.Quality, 0, 1)), dir =>
            {
                GameSettings.Quality = GameSettings.Quality == 0 ? QualitySettings.names.Length - 1 : 0;
                GameSettings.Save();
            }));

            panel.Add(OptionRow("opt.laps", () => GameSettings.Laps.ToString(), dir =>
            {
                GameSettings.Laps = Mathf.Clamp(GameSettings.Laps + dir, GameSettings.MinLaps, GameSettings.MaxLaps);
                GameSettings.Save();
            }));

            var back = UIKit.Button(Loc.T("menu.back"), () => ShowPage(Page.Main), "candy-button--lemon", "candy-button--small");
            panel.Add(back);
            screen.Add(panel);
            UIKit.FocusLater(back);
            return screen;
        }

        /// <summary>"Label   ‹ value ›" row. The row itself is focusable so left/right changes it.</summary>
        VisualElement OptionRow(string labelKey, System.Func<string> value, System.Action<int> change)
        {
            var row = UIKit.Div("option-row");
            row.Add(UIKit.Label(Loc.T(labelKey), "option-row__label"));

            var controls = UIKit.Div("row");
            var valueLabel = UIKit.Label(value(), "option-row__value");
            void Change(int dir) { change(dir); valueLabel.text = value(); UIKit.Pop(valueLabel); }
            controls.Add(UIKit.ArrowButton("‹", () => Change(-1), small: true));
            controls.Add(valueLabel);
            controls.Add(UIKit.ArrowButton("›", () => Change(1), small: true));
            row.Add(controls);

            row.focusable = true;
            row.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction == NavigationMoveEvent.Direction.Left) { Change(-1); e.StopPropagation(); }
                else if (e.direction == NavigationMoveEvent.Direction.Right) { Change(1); e.StopPropagation(); }
            });
            return row;
        }
    }
}
