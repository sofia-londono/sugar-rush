using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
        enum Page { Main, Characters, Options, Online, Lobby, Local }

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
        CandyTitle lobbyKartName, lobbyCount;
        int shownLobbyVersion = -1;
        bool connecting, justJoined;

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
            if (page == Page.Local) { UpdateLocal(); return; }
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
                Page.Local => BuildLocal(),
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

            var play = UIKit.Button(Loc.T("menu.play"), Play, "candy-button--menu");
            column.Add(play);
            column.Add(UIKit.Button(Loc.T("menu.online"), () => ShowPage(Page.Online), "candy-button--sky", "candy-button--menu"));
            if (RaceSetup.SplitScreenAvailable)
                column.Add(UIKit.Button(Loc.T("menu.local"), () => ShowPage(Page.Local), "candy-button--pink-light", "candy-button--menu", "candy-button--long"));
            column.Add(UIKit.Button(Loc.T("menu.characters"), () => ShowPage(Page.Characters), "candy-button--mint", "candy-button--menu"));
            column.Add(UIKit.Button(Loc.T("menu.options"), () => ShowPage(Page.Options), "candy-button--lavender", "candy-button--menu"));
#if !UNITY_WEBGL
            column.Add(UIKit.Button(Loc.T("menu.quit"), Quit, "candy-button--lemon", "candy-button--menu"));
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
            RaceSetup.SetSingle();
            UIKit.FadeOut(root, () => SceneManager.LoadScene(SceneNames.Race));
        }

        // ------------------------------------------------------------ Local split screen

        /// <summary>One of the two seats: which device joined it, its racer and whether it's ready.</summary>
        class LocalSlot
        {
            public InputDevice Device;
            public int Kart;
            public bool Ready;
            public VisualElement Panel, Body;
            public CandyTitle Name;
            public Label DeviceLabel, Status;
        }

        readonly LocalSlot[] localSlots = { new(), new() };

        VisualElement BuildLocal()
        {
            foreach (var slot in localSlots) { slot.Device = null; slot.Ready = false; }

            var screen = UIKit.Div("screen");
            screen.Add(UIKit.Title(Loc.T("local.title"), CandyTone.Rainbow, "candy-title--lg"));

            var row = UIKit.Div("local-slots");
            for (int i = 0; i < localSlots.Length; i++)
            {
                var slot = localSlots[i];
                slot.Panel = new FrostingPanel(i == 0 ? CandyTone.Pink : CandyTone.Sky, 61 + i);
                slot.Panel.AddToClassList("local-slot");
                slot.Panel.Add(UIKit.Title(Loc.T("lobby.short", i + 1), i == 0 ? CandyTone.Pink : CandyTone.Sky, "candy-title--md"));
                slot.Body = UIKit.Div("menu-column");
                slot.Panel.Add(slot.Body);
                row.Add(slot.Panel);
                RefreshLocalSlot(i);
            }
            screen.Add(row);

            screen.Add(UIKit.Label(Loc.T("local.hint"), "small-text"));
            screen.Add(UIKit.Label(Loc.T("local.pressButton"), "small-text"));
            // Not focusable: the A / Enter presses on this page belong to the players' seats.
            var back = UIKit.BackButton(Loc.T("menu.back"), () => ShowPage(Page.Main), "candy-button--lemon", "candy-button--small");
            back.focusable = false;
            screen.Add(back);
            root.focusController?.focusedElement?.Blur();
            return screen;
        }

        void RefreshLocalSlot(int index)
        {
            var slot = localSlots[index];
            slot.Body.Clear();
            slot.Panel.EnableInClassList("local-slot--ready", slot.Ready);
            if (slot.Device == null)
            {
                slot.Body.Add(UIKit.Label(Loc.T("local.join"), "local-slot__waiting"));
                return;
            }
            slot.DeviceLabel = UIKit.Label(DeviceName(slot.Device), "local-slot__device");
            slot.Body.Add(slot.DeviceLabel);
            var nameRow = UIKit.Div("row");
            nameRow.Add(UIKit.ArrowButton("‹", () => CycleLocal(index, -1), small: true));
            // First name only: two seats side by side leave little room ("Adorabeezle Winterpop" won't fit).
            slot.Name = UIKit.Title(roster.Get(slot.Kart).displayName.Split(' ')[0], index == 0 ? CandyTone.Pink : CandyTone.Sky, "candy-title--sm");
            slot.Name.AddToClassList("lobby-pick__name");
            nameRow.Add(slot.Name);
            nameRow.Add(UIKit.ArrowButton("›", () => CycleLocal(index, 1), small: true));
            slot.Body.Add(nameRow);
            slot.Status = UIKit.Label(Loc.T(slot.Ready ? "local.ready" : "local.choose"), "local-slot__status");
            slot.Body.Add(slot.Status);
        }

        static string DeviceName(InputDevice device)
        {
            if (device is Keyboard) return Loc.T("local.keyboard");
            int n = 1;
            foreach (var pad in Gamepad.all) { if (pad == device) break; n++; }
            return Loc.T("local.gamepad", n);
        }

        bool KartTakenByOther(int kart, int slotIndex)
        {
            var other = localSlots[1 - slotIndex];
            return other.Device != null && other.Kart == kart;
        }

        void CycleLocal(int index, int dir)
        {
            var slot = localSlots[index];
            if (slot.Device == null || slot.Ready) return;
            int count = roster.karts.Length;
            for (int step = 1; step <= count; step++)
            {
                int k = ((slot.Kart + dir * step) % count + count) % count;
                if (KartTakenByOther(k, index)) continue;
                slot.Kart = k;
                break;
            }
            AudioHub.UIMove();
            showcase.Show(slot.Kart);
            RefreshLocalSlot(index);
            UIKit.Pop(slot.Name);
        }

        /// <summary>Reads every keyboard / gamepad: join, pick, ready up, back out.</summary>
        void UpdateLocal()
        {
            var devices = new List<InputDevice>();
            if (Keyboard.current != null) devices.Add(Keyboard.current);
            devices.AddRange(Gamepad.all);

            foreach (var device in devices)
            {
                int index = System.Array.FindIndex(localSlots, s => s.Device == device);
                bool confirm = device is Keyboard kb ? kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                    : device is Gamepad gp && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame);
                bool back = device is Keyboard kb2 ? kb2.escapeKey.wasPressedThisFrame || kb2.backspaceKey.wasPressedThisFrame
                    : device is Gamepad gp2 && gp2.buttonEast.wasPressedThisFrame;
                int dir = 0;
                if (device is Keyboard k3)
                {
                    if (k3.leftArrowKey.wasPressedThisFrame || k3.aKey.wasPressedThisFrame) dir--;
                    if (k3.rightArrowKey.wasPressedThisFrame || k3.dKey.wasPressedThisFrame) dir++;
                }
                else if (device is Gamepad g3)
                {
                    if (g3.dpad.left.wasPressedThisFrame || g3.leftStick.left.wasPressedThisFrame) dir--;
                    if (g3.dpad.right.wasPressedThisFrame || g3.leftStick.right.wasPressedThisFrame) dir++;
                }

                if (index < 0)
                {
                    // Not seated yet: A / Enter takes the first free seat; Esc / B leaves the page.
                    if (back && System.Array.TrueForAll(localSlots, s => s.Device == null)) { AudioHub.UIBack(); ShowPage(Page.Main); return; }
                    if (!confirm) continue;
                    int free = System.Array.FindIndex(localSlots, s => s.Device == null);
                    if (free < 0) continue;
                    var slot = localSlots[free];
                    slot.Device = device;
                    slot.Kart = free == 0 ? GameSettings.SelectedKart : 0;
                    if (KartTakenByOther(slot.Kart, free)) CycleLocalSilently(free);
                    AudioHub.UIConfirm();
                    showcase.Show(slot.Kart);
                    RefreshLocalSlot(free);
                    continue;
                }

                var seat = localSlots[index];
                if (dir != 0) CycleLocal(index, dir);
                if (confirm && !seat.Ready)
                {
                    seat.Ready = true;
                    AudioHub.UIConfirm();
                    RefreshLocalSlot(index);
                    UIKit.Pop(seat.Status);
                    if (System.Array.TrueForAll(localSlots, s => s.Ready)) { StartLocalRace(); return; }
                }
                else if (back)
                {
                    AudioHub.UIBack();
                    if (seat.Ready) seat.Ready = false;
                    else seat.Device = null;
                    RefreshLocalSlot(index);
                }
            }
        }

        void CycleLocalSilently(int index)
        {
            int count = roster.karts.Length;
            var slot = localSlots[index];
            for (int k = 0; k < count; k++)
                if (!KartTakenByOther(k, index)) { slot.Kart = k; return; }
        }

        void StartLocalRace()
        {
            if (leaving) return;
            leaving = true;
            GameSettings.SelectedKart = localSlots[0].Kart;
            GameSettings.Save();
            var players = new List<RaceSetup.LocalPlayer>();
            foreach (var slot in localSlots)
                players.Add(new RaceSetup.LocalPlayer { Kart = slot.Kart, Devices = new[] { slot.Device } });
            RaceSetup.SetLocalSplit(players);
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
            var quick = UIKit.Button(Loc.T("online.quick"), QuickJoin);
            onlineButtons.Add(quick);
            onlineButtons.Add(UIKit.Label(Loc.T("online.quickNote"), "panel-note"));

            var createRow = UIKit.Div("row", "create-row");
            createRow.Add(UIKit.Button(Loc.T("online.create"), CreateRoom, "candy-button--sky", "candy-button--small"));
            createRow.Add(OptionRow("online.roomType", () => Loc.T(createPublic ? "online.public" : "online.private"), _ => createPublic = !createPublic));
            onlineButtons.Add(createRow);
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
            UIKit.FocusLater(quick);
            return screen;
        }

        /// <summary>Remembered while the game is open; new rooms start private.</summary>
        static bool createPublic;

        async void QuickJoin()
        {
            if (connecting) return;
            SetConnecting(true, "online.searching");
            bool ok = await OnlineSession.QuickJoinAsync();
            if (!this) return;
            SetConnecting(false);
            if (ok) { justJoined = true; ShowPage(Page.Lobby); }
            else onlineStatus.text = Loc.T(OnlineSession.LastErrorKey ?? "online.error");
        }

        async void CreateRoom()
        {
            if (connecting) return;
            SetConnecting(true);
            bool ok = await OnlineSession.CreateRoomAsync(createPublic);
            if (!this) return;
            SetConnecting(false);
            if (ok) ShowPage(Page.Lobby);
            else onlineStatus.text = Loc.T(OnlineSession.LastErrorKey ?? "online.error");
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
            else onlineStatus.text = Loc.T(OnlineSession.LastErrorKey ?? "online.error");
        }

        void SetConnecting(bool value, string messageKey = "online.connecting")
        {
            connecting = value;
            if (onlineButtons != null) onlineButtons.SetEnabled(!value);
            if (onlineStatus != null) onlineStatus.text = value ? Loc.T(messageKey) : "";
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
            panel.Add(UIKit.Label(Loc.T(OnlineSession.IsPublic ? "lobby.public" : "lobby.private"), "panel-note"));
            // Only right after a quick join: coming back from a race shows the plain room.
            if (justJoined)
                panel.Add(UIKit.Label(Loc.T(OnlineSession.QuickJoinCreated ? "lobby.quickCreated" : "lobby.quickJoined"), "online-status", "lobby-notice"));
            justJoined = false;

            lobbyCount = UIKit.Title("", CandyTone.Lavender, "candy-title--sm");
            panel.Add(lobbyCount);
            lobbyRows = UIKit.Div();
            panel.Add(lobbyRows);
            panel.Add(UIKit.Label(Loc.T("lobby.aiFill"), "panel-note"));
            shownLobbyVersion = -1;

            // Pick a racer here; ones another player already has are skipped.
            var pickRow = UIKit.Div("row", "lobby-pick");
            pickRow.Add(UIKit.Label(Loc.T("lobby.yourRacer"), "option-row__label"));
            pickRow.Add(UIKit.ArrowButton("‹", () => CycleLobbyKart(-1), small: true));
            lobbyKartName = UIKit.Title("", CandyTone.Pink, "candy-title--sm");
            lobbyKartName.AddToClassList("lobby-pick__name");
            pickRow.Add(lobbyKartName);
            pickRow.Add(UIKit.ArrowButton("›", () => CycleLobbyKart(1), small: true));
            panel.Add(pickRow);

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
            int dir = UIKit.HorizontalPressed();
            if (dir != 0) { AudioHub.UIMove(); CycleLobbyKart(dir); }
            var lobby = NetLobby.Instance;
            if (!lobby || lobbyRows == null || lobby.Version == shownLobbyVersion) return;
            shownLobbyVersion = lobby.Version;

            lobbyRows.Clear();
            ulong me = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
            lobbyCount.Text = Loc.T("lobby.count", lobby.Players.Count, OnlineSession.MaxPlayers);
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
                if (p.ClientId == me)
                {
                    showcase.Show(p.Kart);
                    if (lobbyKartName.Text != roster.Get(p.Kart).displayName) UIKit.Pop(lobbyKartName);
                    lobbyKartName.Text = roster.Get(p.Kart).displayName;
                    GameSettings.SelectedKart = p.Kart;
                }
            }
        }

        /// <summary>Asks the host for the next racer nobody else in the room is driving.</summary>
        void CycleLobbyKart(int direction)
        {
            var lobby = NetLobby.Instance;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (!lobby || !nm || !lobby.TryGetPlayer(nm.LocalClientId, out var me)) return;
            int next = lobby.NextFreeKart(me.Kart, direction, nm.LocalClientId);
            if (next != me.Kart) lobby.RequestKartRpc(next);
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

            panel.Add(OptionRow("opt.chaos", () => Loc.T(GameSettings.RalphChaos ? "opt.on" : "opt.off"), dir =>
            {
                GameSettings.RalphChaos = !GameSettings.RalphChaos;
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
