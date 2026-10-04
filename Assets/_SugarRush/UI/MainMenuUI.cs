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
        enum Page { Main, Characters, Options, Online, Lobby }

        public UIDocument document;
        public KartRoster roster;
        public KartShowcase showcase;

        VisualElement root;
        Page page;
        int previewKart;
        bool leaving;

        // Online widgets
        Label onlineStatus;
        TextField codeField;
        VisualElement onlineButtons, lobbyRows;
        int shownLobbyVersion = -1;
        bool connecting;

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
            // Back from an online race: straight to the waiting room.
            ShowPage(OnlineSession.IsOnline ? Page.Lobby : Page.Main);
            UIKit.FadeIn(root);
            if (SoundLibrary.Instance) AudioHub.PlayMusic(SoundLibrary.Instance.menuMusic);
        }

        void Update()
        {
            if (leaving) return;
            if (page == Page.Lobby) UpdateLobby();
            if (page != Page.Main && UIKit.BackPressed() && !connecting && !(codeField != null && codeField.focusController?.focusedElement == codeField))
            {
                AudioHub.UIBack();
                if (page == Page.Lobby) { LeaveRoom(); return; }
                if (page == Page.Characters) previewKart = GameSettings.SelectedKart;
                ShowPage(Page.Main);
                return;
            }

            if (page == Page.Characters)
            {
                int dir = UIKit.HorizontalPressed();
                if (dir != 0) { AudioHub.UIMove(); CycleKart(dir); }
            }
        }

        void ShowPage(Page next)
        {
            page = next;
            var screen = page switch
            {
                Page.Characters => BuildCharacters(),
                Page.Options => BuildOptions(),
                Page.Online => BuildOnline(),
                Page.Lobby => BuildLobby(),
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
            column.Add(UIKit.Button(Loc.T("menu.online"), () => ShowPage(Page.Online), "candy-button--sky"));
            column.Add(UIKit.Button(Loc.T("menu.characters"), () => ShowPage(Page.Characters), "candy-button--mint"));
            column.Add(UIKit.Button(Loc.T("menu.options"), () => ShowPage(Page.Options), "candy-button--lavender"));
#if !UNITY_WEBGL
            column.Add(UIKit.Button(Loc.T("menu.quit"), Quit, "candy-button--lemon"));
#endif

            float best = GameSettings.GetBestTime(GameSettings.Laps);
            if (best > 0f)
                column.Add(UIKit.Label(Loc.T("menu.best", GameSettings.Laps, Loc.Time(best)), "small-text"));

            if (!string.IsNullOrEmpty(OnlineSession.PendingMessageKey))
            {
                column.Add(UIKit.Label(Loc.T(OnlineSession.PendingMessageKey), "menu-message"));
                OnlineSession.PendingMessageKey = null;
            }

            screen.Add(UIKit.Label(Loc.T("menu.credits"), "small-text", "footer"));
            UIKit.FocusLater(play);
            return screen;
        }

        void Play()
        {
            if (leaving) return;
            leaving = true;
            AudioHub.UIConfirm();
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

        // ------------------------------------------------------------ Online

        VisualElement BuildOnline()
        {
            var screen = UIKit.Div("screen");
            var panel = new FrostingPanel(CandyTone.Sky, 19);
            panel.AddToClassList("frosting-panel--wide");
            var heading = UIKit.Title(Loc.T("online.title"), CandyTone.Pink, "candy-title--md");
            heading.AddToClassList("panel-heading");
            panel.Add(heading);

            onlineButtons = UIKit.Div("menu-column");
            var create = UIKit.Button(Loc.T("online.create"), CreateRoom, "candy-button--sky");
            onlineButtons.Add(create);
            onlineButtons.Add(UIKit.Label(Loc.T("online.or"), "panel-note"));

            var joinRow = UIKit.Div("row");
            codeField = new TextField { maxLength = 8, isDelayed = false };
            codeField.AddToClassList("candy-input");
            codeField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) JoinRoom();
            });
            joinRow.Add(codeField);
            joinRow.Add(UIKit.Button(Loc.T("online.join"), JoinRoom, "candy-button--mint", "candy-button--small"));
            onlineButtons.Add(joinRow);
            panel.Add(onlineButtons);

            onlineStatus = UIKit.Label("", "online-status");
            panel.Add(onlineStatus);
            panel.Add(UIKit.BackButton(Loc.T("menu.back"), () => ShowPage(Page.Main), "candy-button--lemon", "candy-button--small"));

            screen.Add(panel);
            UIKit.FocusLater(create);
            return screen;
        }

        async void CreateRoom()
        {
            if (connecting) return;
            SetConnecting(true);
            bool ok = await OnlineSession.CreateRoomAsync();
            if (!this) return;
            SetConnecting(false);
            if (ok) ShowPage(Page.Lobby);
            else onlineStatus.text = Loc.T("online.error");
        }

        async void JoinRoom()
        {
            if (connecting || codeField == null) return;
            string code = codeField.value?.Trim();
            if (string.IsNullOrEmpty(code)) { codeField.Focus(); return; }
            SetConnecting(true);
            bool ok = await OnlineSession.JoinRoomAsync(code);
            if (!this) return;
            SetConnecting(false);
            if (ok) ShowPage(Page.Lobby);
            else onlineStatus.text = Loc.T("online.error");
        }

        void SetConnecting(bool value)
        {
            connecting = value;
            if (onlineButtons != null) onlineButtons.SetEnabled(!value);
            if (onlineStatus != null) onlineStatus.text = value ? Loc.T("online.connecting") : "";
        }

        VisualElement BuildLobby()
        {
            var screen = UIKit.Div("screen", "screen--left");
            var panel = new FrostingPanel(CandyTone.Mint, 29);
            var heading = UIKit.Title(Loc.T("lobby.title"), CandyTone.Pink, "candy-title--md");
            heading.AddToClassList("panel-heading");
            panel.Add(heading);

            var code = UIKit.Title(OnlineSession.Code ?? "------", CandyTone.Lemon, "candy-title--lg");
            code.AddToClassList("room-code");
            panel.Add(code);
            panel.Add(UIKit.Label(Loc.T("lobby.share"), "panel-note"));

            lobbyRows = UIKit.Div();
            panel.Add(lobbyRows);
            panel.Add(UIKit.Label(Loc.T("lobby.aiFill"), "panel-note"));
            shownLobbyVersion = -1;

            var buttons = UIKit.Div("row");
            Button focus;
            if (OnlineSession.IsHost)
            {
                focus = UIKit.Button(Loc.T("lobby.start"), StartOnlineRace, "candy-button--small");
                buttons.Add(UIKit.BackButton(Loc.T("lobby.leave"), LeaveRoom, "candy-button--lemon", "candy-button--small"));
                buttons.Add(focus);
            }
            else
            {
                panel.Add(UIKit.Label(Loc.T("lobby.waiting"), "online-status"));
                focus = UIKit.BackButton(Loc.T("lobby.leave"), LeaveRoom, "candy-button--lemon", "candy-button--small");
                buttons.Add(focus);
            }
            panel.Add(buttons);
            screen.Add(panel);
            UIKit.FocusLater(focus);
            return screen;
        }

        void UpdateLobby()
        {
            if (!OnlineSession.IsOnline) { ShowPage(Page.Main); return; }
            var lobby = NetLobby.Instance;
            if (!lobby || lobbyRows == null || lobby.Version == shownLobbyVersion) return;
            shownLobbyVersion = lobby.Version;

            lobbyRows.Clear();
            ulong me = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
            foreach (var p in lobby.Players)
            {
                var row = UIKit.Div("results-row");
                if (p.ClientId == me) row.AddToClassList("results-row--player");
                row.Add(UIKit.Label(p.Number + ".", "results-row__pos"));
                row.Add(UIKit.Label(roster.Get(p.Kart).displayName, "results-row__name"));
                string tag = p.ClientId == me ? Loc.T("lobby.you") : Loc.T("lobby.player", p.Number);
                if (p.ClientId == Unity.Netcode.NetworkManager.ServerClientId) tag += " · " + Loc.T("lobby.host");
                row.Add(UIKit.Label(tag, "results-row__time"));
                lobbyRows.Add(row);
                if (p.ClientId == me) showcase.Show(p.Kart);
            }
        }

        void StartOnlineRace()
        {
            if (leaving || !NetLobby.Instance) return;
            leaving = true;
            AudioHub.UIConfirm();
            GameSettings.Save();
            UIKit.FadeOut(root, () => NetLobby.Instance.StartRace());
        }

        void LeaveRoom()
        {
            if (leaving) return;
            leaving = true;
            UIKit.FadeOut(root, () => OnlineSession.LeaveToMenu());
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
            buttons.Add(UIKit.BackButton(Loc.T("menu.back"), () => { previewKart = GameSettings.SelectedKart; ShowPage(Page.Main); },
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

            panel.Add(OptionRow("opt.difficulty", () => Loc.T("opt.difficulty." + (int)GameSettings.Difficulty), dir =>
            {
                GameSettings.Difficulty = (Difficulty)Mathf.Clamp((int)GameSettings.Difficulty + dir, 0, 2);
                GameSettings.Save();
            }));

            panel.Add(OptionRow("opt.music", () => Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%", dir =>
            {
                GameSettings.MusicVolume = Mathf.Clamp01(Mathf.Round(GameSettings.MusicVolume * 10f + dir) / 10f);
                GameSettings.Save();
            }));

            panel.Add(OptionRow("opt.sfx", () => Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%", dir =>
            {
                GameSettings.SfxVolume = Mathf.Clamp01(Mathf.Round(GameSettings.SfxVolume * 10f + dir) / 10f);
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

            var back = UIKit.BackButton(Loc.T("menu.back"), () => ShowPage(Page.Main), "candy-button--lemon", "candy-button--small");
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
            void Change(int dir) { change(dir); valueLabel.text = value(); UIKit.Pop(valueLabel); AudioHub.UIMove(); }
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
