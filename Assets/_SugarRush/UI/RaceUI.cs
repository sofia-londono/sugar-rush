using UnityEngine;
using UnityEngine.UIElements;

namespace SugarRush
{
    /// <summary>
    /// In-race UI: HUD (position, lap, time, speed), countdown and messages, the
    /// "back to track" prompt, the pause menu and the results screen — all in the candy style.
    /// </summary>
    public class RaceUI : MonoBehaviour
    {
        public UIDocument document;

        RaceManager race;
        VisualElement root, hud, overlay, lost;
        CandyTitle position, positionTotal, speed, boost, countdown, message;
        Label lap, time, lostText, pauseHint;
        VisualElement resultsRows;
        float messageUntil;
        float resultsRefresh;
        bool resultsShown, leaving, finalLapAnnounced;
        int lastCountdown = -1;

        void Start()
        {
            race = RaceManager.Instance;
            root = document.rootVisualElement;
            root.Clear();
            BuildHud();
            race.StateChanged += OnStateChanged;
            race.RacerLapCompleted += OnLapCompleted;
            UIKit.FadeIn(root);
        }

        void OnDestroy()
        {
            if (!race) return;
            race.StateChanged -= OnStateChanged;
            race.RacerLapCompleted -= OnLapCompleted;
        }

        // ------------------------------------------------------------ HUD

        void BuildHud()
        {
            hud = UIKit.Div("hud");

            var topLeft = UIKit.Div("hud-corner", "hud-corner--top-left");
            var posRow = UIKit.Div("hud-row");
            position = UIKit.Title("", CandyTone.Pink, "candy-title--xl");
            positionTotal = UIKit.Title("", CandyTone.Lavender, "candy-title--sm");
            positionTotal.AddToClassList("hud-total");
            posRow.Add(position);
            posRow.Add(positionTotal);
            topLeft.Add(posRow);
            hud.Add(topLeft);

            var topRight = UIKit.Div("hud-corner", "hud-corner--top-right");
            lap = UIKit.Chip(topRight, "", "candy-chip--lap");
            time = UIKit.Chip(topRight, "", "candy-chip--time");
            hud.Add(topRight);

            var bottomRight = UIKit.Div("hud-corner", "hud-corner--bottom-right");
            boost = UIKit.Title("¡TURBO!", CandyTone.Lemon, "candy-title--sm");
            var speedRow = UIKit.Div("hud-row");
            speed = UIKit.Title("0", CandyTone.Sky, "candy-title--lg");
            speed.AddToClassList("hud-speed");
            speedRow.Add(speed);
            speedRow.Add(UIKit.Label("km/h", "hud-unit"));
            bottomRight.Add(boost);
            bottomRight.Add(speedRow);
            hud.Add(bottomRight);

            var bottomLeft = UIKit.Div("hud-corner", "hud-corner--bottom-left");
            pauseHint = UIKit.Label(Loc.T("hud.pauseHint"), "small-text");
            bottomLeft.Add(pauseHint);
            hud.Add(bottomLeft);

            var center = UIKit.Div("hud-center");
            countdown = UIKit.Title("", CandyTone.Rainbow, "candy-title--xxl");
            message = UIKit.Title("", CandyTone.Rainbow, "candy-title--lg");
            lost = new FrostingPanel(CandyTone.Mint, 23);
            lost.AddToClassList("frosting-panel--compact");
            lost.AddToClassList("hud-lost");
            lostText = UIKit.Label("", "hud-lost__text");
            lost.Add(lostText);
            center.Add(countdown);
            center.Add(message);
            center.Add(lost);
            hud.Add(center);

            foreach (var e in hud.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            root.Add(hud);
        }

        void Update()
        {
            if (!race || !race.Player) return;

            if (UIKit.PausePressed() && !resultsShown && !leaving)
                SetPaused(!race.IsPaused);

            RefreshHud();

            if (resultsShown)
            {
                resultsRefresh -= Time.unscaledDeltaTime;
                if (resultsRefresh <= 0f) { resultsRefresh = 0.5f; RefreshResults(); }
            }
        }

        void RefreshHud()
        {
            var player = race.Player;
            position.Text = Loc.Ordinal(player.Position);
            positionTotal.Text = "/" + race.Racers.Count;
            lap.text = Loc.T("hud.lap", Mathf.Clamp(player.CompletedLaps + 1, 1, race.Laps), race.Laps);
            time.text = Loc.Time(player.Finished ? player.FinishTime : race.RaceTime);
            speed.Text = Mathf.RoundToInt(player.Kart.Speed * 3.6f).ToString();
            boost.EnableInClassList("hidden", !player.Kart.IsBoosting);

            bool counting = race.CurrentState == RaceManager.State.Countdown;
            countdown.EnableInClassList("hidden", !counting);
            if (counting && race.Countdown != lastCountdown)
            {
                lastCountdown = race.Countdown;
                countdown.Text = race.Countdown.ToString();
                UIKit.Pop(countdown);
            }

            // Priority: wrong way warning, then timed messages.
            bool wrongWay = player.WrongWay && !player.Finished;
            if (wrongWay)
            {
                message.Text = Loc.T("hud.wrongWay");
                message.Tone = CandyTone.Lemon;
            }
            message.EnableInClassList("hidden", !wrongWay && Time.unscaledTime > messageUntil);

            bool showLost = player.NeedsHelp && !player.Finished && !race.IsPaused;
            lost.EnableInClassList("hidden", !showLost);
            if (showLost)
                lostText.text = Loc.T("hud.lost") + "\n" + Loc.T("hud.autoReturn", Mathf.CeilToInt(Mathf.Max(0f, player.AutoReturnIn)));

            pauseHint.EnableInClassList("hidden", resultsShown);
        }

        void ShowMessage(string text, float seconds = 1.6f)
        {
            message.Tone = CandyTone.Rainbow;
            message.Text = text;
            messageUntil = Time.unscaledTime + seconds;
            UIKit.Pop(message);
        }

        void OnStateChanged(RaceManager.State state)
        {
            if (state == RaceManager.State.Racing) ShowMessage(Loc.T("hud.go"), 1.2f);
            if (state == RaceManager.State.Finished) Invoke(nameof(ShowResults), 1.5f);
        }

        void OnLapCompleted(RaceProgress racer)
        {
            if (!racer.isPlayer || racer.Finished) return;
            if (racer.CompletedLaps == race.Laps - 1 && !finalLapAnnounced)
            {
                finalLapAnnounced = true;
                ShowMessage(Loc.T("hud.finalLap"), 2.2f);
            }
            else ShowMessage(Loc.T("hud.lapTime", racer.CompletedLaps, Loc.Time(racer.LastLap)), 2f);
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
            panel.Add(UIKit.Button(Loc.T("pause.restart"), () => LeaveTo(race.Restart), "candy-button--mint"));
            panel.Add(UIKit.Button(Loc.T("pause.checkpoint"), () => { CloseOverlay(); race.PlayerBackToTrack(); }, "candy-button--lavender"));
            panel.Add(UIKit.Button(Loc.T("pause.mainMenu"), () => LeaveTo(race.QuitToMenu), "candy-button--lemon"));
            overlay.Add(panel);
            UIKit.Enter(root, overlay);
            UIKit.FocusLater(resume);
        }

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
            var player = race.Player;

            overlay = UIKit.Div("screen", "screen--dim");
            overlay.Add(new SprinkleRain(60));
            var panel = new FrostingPanel(CandyTone.Pink, 47);
            panel.AddToClassList("frosting-panel--wide");
            panel.Add(UIKit.Title(Loc.T("results.title"), CandyTone.Pink, "candy-title--md"));
            panel.Add(UIKit.Title(Loc.Ordinal(player.Position), CandyTone.Lavender, "candy-title--lg"));
            if (race.NewRecord) panel.Add(UIKit.Label(Loc.T("results.newRecord"), "results-highlight"));

            resultsRows = UIKit.Div();
            panel.Add(resultsRows);
            panel.Add(UIKit.Label(Loc.T("results.bestLap", Loc.Time(player.BestLap)), "results-highlight"));

            var buttons = UIKit.Div("row");
            var again = UIKit.Button(Loc.T("results.retry"), () => LeaveTo(race.Restart), "candy-button--small");
            buttons.Add(again);
            buttons.Add(UIKit.Button(Loc.T("pause.mainMenu"), () => LeaveTo(race.QuitToMenu), "candy-button--lemon", "candy-button--small"));
            panel.Add(buttons);

            overlay.Add(panel);
            UIKit.Enter(root, overlay);
            RefreshResults();
            UIKit.FocusLater(again);
        }

        void RefreshResults()
        {
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
