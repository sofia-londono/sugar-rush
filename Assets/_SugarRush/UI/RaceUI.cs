using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SugarRush
{
    /// <summary>
    /// In-race UI: one HUD per local player (position, lap, time, speed, messages, "back to
    /// track" prompt) — full screen normally, side by side in split screen — plus the shared
    /// countdown, the pause menu and the results screen, all in the candy style.
    /// </summary>
    public class RaceUI : MonoBehaviour
    {
        public UIDocument document;

        /// <summary>The HUD of one player, laid over their half (or all) of the screen.</summary>
        class PlayerHud
        {
            public RaceProgress Player;
            public VisualElement Root, Lost;
            public CandyTitle Position, Total, Speed, Boost, Message, Tag, Hammer;
            public Label Lap, Time, LostText, Coins;
            public VisualElement CoinRow;
            public float MessageUntil;
            public bool FinalLapAnnounced;

            public void ShowMessage(string text, float seconds, CandyTone tone = CandyTone.Rainbow)
            {
                Message.Tone = tone;
                Message.Text = text;
                MessageUntil = UnityEngine.Time.unscaledTime + seconds;
                UIKit.Pop(Message);
            }
        }

        RaceManager race;
        RalphChaos chaos;
        ResultsPodium podium;
        VisualElement podiumView;
        VisualElement root, overlay, sharedCenter, bannerBox;
        CandyTitle countdown, banner;
        float bannerUntil;
        Label pauseHint;
        readonly List<PlayerHud> huds = new();
        VisualElement resultsRows;
        float resultsRefresh;
        bool resultsShown, leaving;
        int lastCountdown = -1;

        static SoundLibrary Lib => SoundLibrary.Instance;

        void Start()
        {
            race = RaceManager.Instance;
            root = document.rootVisualElement;
            root.Clear();
            BuildShared();
            race.StateChanged += OnStateChanged;
            race.RacerLapCompleted += OnLapCompleted;
            race.LocalPlayerFinished += OnLocalPlayerFinished;
            chaos = RalphChaos.Instance;
            podium = FindFirstObjectByType<ResultsPodium>(FindObjectsInactive.Include);
            if (chaos)
            {
                chaos.RalphIncoming += OnRalphIncoming;
                chaos.Repaired += OnRepaired;
                chaos.CoinCollected += OnCoinCollected;
            }
            UIKit.FadeIn(root);
            if (Lib) AudioHub.PlayMusic(Lib.raceMusic);
        }

        void OnDestroy()
        {
            if (!race) return;
            race.StateChanged -= OnStateChanged;
            race.RacerLapCompleted -= OnLapCompleted;
            race.LocalPlayerFinished -= OnLocalPlayerFinished;
            if (chaos)
            {
                chaos.RalphIncoming -= OnRalphIncoming;
                chaos.Repaired -= OnRepaired;
                chaos.CoinCollected -= OnCoinCollected;
            }
        }

        // ------------------------------------------------------------ HUD

        void BuildShared()
        {
            sharedCenter = UIKit.Div("hud-center");
            sharedCenter.pickingMode = PickingMode.Ignore;
            countdown = UIKit.Title("", CandyTone.Rainbow, "candy-title--xxl");
            sharedCenter.Add(countdown);
            root.Add(sharedCenter);

            bannerBox = UIKit.Div("hud-banner", "hidden");
            bannerBox.pickingMode = PickingMode.Ignore;
            banner = UIKit.Title(Loc.T("chaos.incoming"), CandyTone.Lemon, "candy-title--xl");
            bannerBox.Add(banner);
            root.Add(bannerBox);

            var hint = UIKit.Div("hud-corner", "hud-corner--bottom-left");
            hint.pickingMode = PickingMode.Ignore;
            pauseHint = UIKit.Label(Loc.T("hud.pauseHint"), "small-text");
            hint.Add(pauseHint);
            root.Add(hint);
        }

        /// <summary>Creates one HUD per local player once they exist (online karts arrive late).</summary>
        void EnsureHuds()
        {
            if (huds.Count == race.LocalPlayers.Count && (huds.Count == 0 || huds[0].Player == race.LocalPlayers[0])) return;
            foreach (var h in huds) h.Root.RemoveFromHierarchy();
            huds.Clear();

            bool split = race.LocalPlayers.Count > 1;
            for (int i = 0; i < race.LocalPlayers.Count; i++)
            {
                var hud = BuildHud(race.LocalPlayers[i], split, i);
                huds.Add(hud);
                root.Insert(0, hud.Root); // under the shared countdown and menus
            }
            if (split && root.Q(className: "split-divider") == null)
            {
                var divider = UIKit.Div("split-divider");
                divider.pickingMode = PickingMode.Ignore;
                root.Insert(huds.Count, divider);
            }
        }

        PlayerHud BuildHud(RaceProgress player, bool split, int index)
        {
            var h = new PlayerHud { Player = player };
            h.Root = UIKit.Div("hud");
            if (split) h.Root.AddToClassList(index == 0 ? "hud--left" : "hud--right");

            string big = split ? "candy-title--lg" : "candy-title--xl";
            string mid = split ? "candy-title--md" : "candy-title--lg";

            var topLeft = UIKit.Div("hud-corner", "hud-corner--top-left");
            if (split)
            {
                h.Tag = UIKit.Title(Loc.T("lobby.short", index + 1), index == 0 ? CandyTone.Pink : CandyTone.Sky, "candy-title--sm");
                topLeft.Add(h.Tag);
            }
            var posRow = UIKit.Div("hud-row");
            h.Position = UIKit.Title("", CandyTone.Pink, big);
            h.Total = UIKit.Title("", CandyTone.Lavender, "candy-title--sm");
            h.Total.AddToClassList("hud-total");
            posRow.Add(h.Position);
            posRow.Add(h.Total);
            topLeft.Add(posRow);

            // Ralph's chaos: coins carried, and Felix's hammer once there are enough.
            h.CoinRow = UIKit.Div("hud-row", "coin-row");
            h.CoinRow.Add(UIKit.Div("coin-icon"));
            h.Coins = UIKit.Chip(h.CoinRow, "0/5", "candy-chip--coins");
            h.Hammer = UIKit.Title(Loc.T("chaos.hammer"), CandyTone.Lemon, "candy-title--sm");
            h.Hammer.AddToClassList("coin-row__hammer");
            h.CoinRow.Add(h.Hammer);
            topLeft.Add(h.CoinRow);
            h.Root.Add(topLeft);

            var topRight = UIKit.Div("hud-corner", "hud-corner--top-right");
            h.Lap = UIKit.Chip(topRight, "", "candy-chip--lap");
            h.Time = UIKit.Chip(topRight, "", "candy-chip--time");
            h.Root.Add(topRight);

            var bottomRight = UIKit.Div("hud-corner", "hud-corner--bottom-right");
            h.Boost = UIKit.Title("¡TURBO!", CandyTone.Lemon, "candy-title--sm");
            var speedRow = UIKit.Div("hud-row");
            h.Speed = UIKit.Title("0", CandyTone.Sky, mid);
            h.Speed.AddToClassList("hud-speed");
            speedRow.Add(h.Speed);
            speedRow.Add(UIKit.Label("km/h", "hud-unit"));
            bottomRight.Add(h.Boost);
            bottomRight.Add(speedRow);
            h.Root.Add(bottomRight);

            var center = UIKit.Div("hud-center", "hud-center--player");
            h.Message = UIKit.Title("", CandyTone.Rainbow, split ? "candy-title--md" : "candy-title--lg");
            h.Lost = new FrostingPanel(CandyTone.Mint, 23 + index);
            h.Lost.AddToClassList("frosting-panel--compact");
            h.Lost.AddToClassList("hud-lost");
            h.LostText = UIKit.Label("", "hud-lost__text");
            h.Lost.Add(h.LostText);
            center.Add(h.Message);
            center.Add(h.Lost);
            h.Root.Add(center);

            foreach (var e in h.Root.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            h.Root.pickingMode = PickingMode.Ignore;
            return h;
        }

        void Update()
        {
            if (!race) return;
            EnsureHuds();
            RefreshCountdown();
            if (huds.Count == 0) return;

            if (UIKit.PausePressed() && !resultsShown && !leaving)
            {
                if (race.IsPaused) AudioHub.UIBack(); else AudioHub.UIConfirm();
                SetPaused(!race.IsPaused);
            }

            foreach (var h in huds) RefreshHud(h);
            pauseHint.EnableInClassList("hidden", resultsShown);
            RefreshBanner();

            if (resultsShown)
            {
                resultsRefresh -= Time.unscaledDeltaTime;
                if (resultsRefresh <= 0f) { resultsRefresh = 0.5f; RefreshResults(); }
            }
        }

        void RefreshCountdown()
        {
            bool counting = race.CurrentState == RaceManager.State.Countdown && race.Countdown > 0 && huds.Count > 0;
            countdown.EnableInClassList("hidden", !counting);
            if (!counting || race.Countdown == lastCountdown) return;
            lastCountdown = race.Countdown;
            countdown.Text = race.Countdown.ToString();
            UIKit.Pop(countdown);
            if (Lib) AudioHub.PlayUI(Lib.countdownBeep, 0.8f);
        }

        void RefreshHud(PlayerHud h)
        {
            var player = h.Player;
            if (!player) return;
            h.Position.Text = Loc.Ordinal(player.Position);
            h.Total.Text = "/" + race.Racers.Count;
            h.Lap.text = Loc.T("hud.lap", Mathf.Clamp(player.CompletedLaps + 1, 1, race.Laps), race.Laps);
            h.Time.text = Loc.Time(player.Finished ? player.FinishTime : race.RaceTime);
            h.Speed.Text = Mathf.RoundToInt(player.Kart.Speed * 3.6f).ToString();
            h.Boost.EnableInClassList("hidden", !player.Kart.IsBoosting);

            bool chaosOn = chaos && chaos.Active;
            h.CoinRow.EnableInClassList("hidden", !chaosOn);
            if (chaosOn)
            {
                int coins = chaos.CoinsOf(player);
                h.Coins.text = $"{coins}/{chaos.hammerCost}";
                bool hammer = coins >= chaos.hammerCost;
                h.Hammer.EnableInClassList("hidden", !hammer);
                // Gentle pulse so a ready hammer is noticed.
                if (hammer) h.Hammer.style.scale = new Scale(Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f)));
            }

            // Priority: wrong way warning, then timed messages.
            bool wrongWay = player.WrongWay && !player.Finished;
            if (wrongWay)
            {
                h.Message.Text = Loc.T("hud.wrongWay");
                h.Message.Tone = CandyTone.Lemon;
            }
            h.Message.EnableInClassList("hidden", !wrongWay && Time.unscaledTime > h.MessageUntil);

            bool showLost = player.NeedsHelp && !player.Finished && !race.IsPaused;
            h.Lost.EnableInClassList("hidden", !showLost);
            if (showLost)
                h.LostText.text = Loc.T("hud.lost") + "\n" + Loc.T("hud.autoReturn", Mathf.CeilToInt(Mathf.Max(0f, player.AutoReturnIn)));
        }

        PlayerHud HudOf(RaceProgress racer) => huds.Find(h => h.Player == racer);

        // ------------------------------------------------------------ Ralph's chaos

        void OnRalphIncoming(int zone)
        {
            bannerUntil = Time.unscaledTime + (chaos ? chaos.warningTime : 2.5f) + 0.8f;
            banner.Text = Loc.T("chaos.incoming");
            bannerBox.RemoveFromClassList("hidden");
            UIKit.Pop(banner);
        }

        void RefreshBanner()
        {
            bool show = Time.unscaledTime < bannerUntil && !resultsShown;
            bannerBox.EnableInClassList("hidden", !show);
            if (!show) return;
            // Shaky letters, like the ground under Ralph's feet.
            float t = Time.unscaledTime * 30f;
            banner.style.translate = new Translate((Mathf.PerlinNoise(t, 0f) - 0.5f) * 16f, (Mathf.PerlinNoise(0f, t) - 0.5f) * 12f);
        }

        void OnRepaired(RaceProgress racer) => HudOf(racer)?.ShowMessage(Loc.T("chaos.fixed"), 1.6f, CandyTone.Lemon);

        void OnCoinCollected(RaceProgress racer)
        {
            var h = HudOf(racer);
            if (h != null) UIKit.Pop(h.CoinRow);
        }

        void OnStateChanged(RaceManager.State state)
        {
            if (state == RaceManager.State.Racing)
            {
                foreach (var h in huds) h.ShowMessage(Loc.T("hud.go"), 1.2f);
                if (Lib) AudioHub.PlayUI(Lib.countdownGo, 0.9f);
            }
            if (state == RaceManager.State.Finished)
            {
                if (Lib) AudioHub.PlayUI(Lib.finishFanfare, 0.9f);
                Invoke(nameof(ShowResults), 1.5f);
            }
        }

        void OnLapCompleted(RaceProgress racer)
        {
            var h = HudOf(racer);
            if (h == null || racer.Finished) return;
            if (Lib) AudioHub.PlayUI(Lib.lapChime, 0.8f, racer.CompletedLaps == race.Laps - 1 ? 1.2f : 1f);
            if (racer.CompletedLaps == race.Laps - 1 && !h.FinalLapAnnounced)
            {
                h.FinalLapAnnounced = true;
                h.ShowMessage(Loc.T("hud.finalLap"), 2.2f);
            }
            else h.ShowMessage(Loc.T("hud.lapTime", racer.CompletedLaps, Loc.Time(racer.LastLap)), 2f);
        }

        /// <summary>Split screen: the first player to finish sees their place while the other races on.</summary>
        void OnLocalPlayerFinished(RaceProgress racer)
        {
            var h = HudOf(racer);
            if (h == null || huds.Count < 2) return;
            h.ShowMessage(Loc.T("hud.finished", Loc.Ordinal(racer.Position)), 999f);
            if (Lib) AudioHub.PlayUI(Lib.lapChime, 0.9f, 1.3f);
        }

        // ------------------------------------------------------------ Pause

        void SetPaused(bool paused)
        {
            race.SetPaused(paused);
            if (paused) ShowPause();
            else CloseOverlay();
        }

        void ShowPause()
        {
            CloseOverlay();
            overlay = UIKit.Div("screen", "screen--dim");
            var panel = new FrostingPanel(CandyTone.Pink, 31);
            var heading = UIKit.Title(Loc.T("pause.title"), CandyTone.Pink, "candy-title--lg");
            heading.AddToClassList("panel-heading");
            panel.Add(heading);
            var resume = UIKit.Button(Loc.T("pause.resume"), () => SetPaused(false));
            panel.Add(resume);
            bool guest = race.IsOnline && !OnlineSession.IsHost;
            if (!guest) panel.Add(UIKit.Button(Loc.T("pause.restart"), () => LeaveTo(race.Restart), "candy-button--mint"));
            panel.Add(UIKit.Button(Loc.T("pause.checkpoint"), () => { CloseOverlay(); race.PlayerBackToTrack(); }, "candy-button--lavender"));
            panel.Add(UIKit.BackButton(Loc.T(QuitKey), () => LeaveTo(race.QuitToMenu), "candy-button--lemon"));
            overlay.Add(panel);
            UIKit.Enter(root, overlay);
            UIKit.FocusLater(resume);
        }

        /// <summary>Online the host takes everyone back to the room, a guest leaves it.</summary>
        string QuitKey => !race.IsOnline ? "pause.mainMenu" : OnlineSession.IsHost ? "lobby.backToRoom" : "lobby.leave";

        void CloseOverlay()
        {
            UIKit.Leave(overlay);
            overlay = null;
        }

        /// <summary>Fades to the pastel curtain before restarting or leaving the race.</summary>
        void LeaveTo(System.Action action)
        {
            if (leaving) return;
            leaving = true;
            UIKit.FadeOut(root, action);
        }

        // ------------------------------------------------------------ Results

        void ShowResults()
        {
            CloseOverlay();
            resultsShown = true;
            foreach (var h in huds) h.Message.AddToClassList("hidden");

            overlay = UIKit.Div("screen", "screen--dim");
            overlay.Add(new SprinkleRain(60));
            var layout = UIKit.Div("results-layout");
            overlay.Add(layout);
            // The top three on a candy podium, filmed live by the podium's own camera.
            if (podium)
            {
                podiumView = UIKit.Div("results-podium");
                podiumView.pickingMode = PickingMode.Ignore;
                layout.Add(podiumView);
            }
            var panel = new FrostingPanel(CandyTone.Pink, 47);
            panel.AddToClassList("frosting-panel--wide");
            panel.AddToClassList("results-panel");
            panel.Add(UIKit.Title(Loc.T("results.title"), CandyTone.Pink, "candy-title--md"));

            // One place per local player ("J1 2º  ·  J2 4º" in split screen).
            var places = UIKit.Div("row");
            for (int i = 0; i < race.LocalPlayers.Count; i++)
            {
                var p = race.LocalPlayers[i];
                string text = race.LocalPlayers.Count > 1 ? $"{Loc.T("lobby.short", i + 1)} {Loc.Ordinal(p.Position)}" : Loc.Ordinal(p.Position);
                var place = UIKit.Title(text, i == 0 ? CandyTone.Lavender : CandyTone.Sky, "candy-title--lg");
                place.AddToClassList("results-place");
                places.Add(place);
            }
            panel.Add(places);
            if (race.NewRecord) panel.Add(UIKit.Label(Loc.T("results.newRecord"), "results-highlight"));

            resultsRows = UIKit.Div();
            panel.Add(resultsRows);
            if (race.Player)
                panel.Add(UIKit.Label(Loc.T("results.bestLap", Loc.Time(BestLocalLap())), "results-highlight"));

            var buttons = UIKit.Div("row");
            Button again;
            if (race.IsOnline && !OnlineSession.IsHost)
            {
                panel.Add(UIKit.Label(Loc.T("lobby.waiting"), "panel-note"));
                again = UIKit.BackButton(Loc.T("lobby.leave"), () => LeaveTo(race.QuitToMenu), "candy-button--lemon", "candy-button--small");
                buttons.Add(again);
            }
            else
            {
                again = UIKit.Button(Loc.T("results.retry"), () => LeaveTo(race.Restart), "candy-button--small");
                buttons.Add(again);
                buttons.Add(UIKit.BackButton(Loc.T(QuitKey), () => LeaveTo(race.QuitToMenu), "candy-button--lemon", "candy-button--small"));
            }
            panel.Add(buttons);

            layout.Add(panel);
            UIKit.Enter(root, overlay);
            RefreshResults();
            UIKit.FocusLater(again);
        }

        float BestLocalLap()
        {
            float best = 0f;
            foreach (var p in race.LocalPlayers)
                if (p.BestLap > 0f && (best <= 0f || p.BestLap < best)) best = p.BestLap;
            return best;
        }

        void RefreshResults()
        {
            if (podium && podiumView != null)
            {
                var top = new List<int>();
                foreach (var r in race.Racers) if (top.Count < 3) top.Add(r.kartIndex);
                var view = podium.Show(top);
                podiumView.style.backgroundImage = Background.FromRenderTexture(view);
            }
            resultsRows.Clear();
            foreach (var r in race.Racers)
            {
                var row = UIKit.Div("results-row");
                if (r.isPlayer) row.AddToClassList("results-row--player");
                row.Add(UIKit.Label(Loc.Ordinal(r.Position), "results-row__pos"));
                row.Add(UIKit.Label(r.racerName, "results-row__name"));
                row.Add(UIKit.Label(r.Finished ? Loc.Time(r.FinishTime) : Loc.T("results.racing"), "results-row__time"));
                resultsRows.Add(row);
            }
        }
    }
}
