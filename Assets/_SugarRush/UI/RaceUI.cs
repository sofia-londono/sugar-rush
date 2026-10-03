using UnityEngine;
using UnityEngine.UIElements;

namespace SugarRush
{
    /// <summary>
    /// In-race UI: HUD (position, lap, time, speed), countdown and messages, the
    /// "back to track" prompt, the pause menu and the results screen.
    /// </summary>
    public class RaceUI : MonoBehaviour
    {
        public UIDocument document;

        RaceManager race;
        VisualElement root, hud, overlay;
        Label position, positionTotal, lap, time, speed, boost, countdown, message, lost, pauseHint;
        VisualElement resultsRows;
        float messageUntil;
        float resultsRefresh;
        bool resultsShown;
        bool finalLapAnnounced;

        void Start()
        {
            race = RaceManager.Instance;
            root = document.rootVisualElement;
            root.Clear();
            BuildHud();
            race.StateChanged += OnStateChanged;
            race.RacerLapCompleted += OnLapCompleted;
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
            hud.pickingMode = PickingMode.Ignore;

            var topLeft = UIKit.Div("hud-corner", "hud-corner--top-left");
            var posRow = UIKit.Div("row");
            position = UIKit.Label("", "hud-position");
            positionTotal = UIKit.Label("", "hud-position__total");
            posRow.Add(position);
            posRow.Add(positionTotal);
            topLeft.Add(posRow);
            hud.Add(topLeft);

            var topRight = UIKit.Div("hud-corner", "hud-corner--top-right");
            lap = UIKit.Label("", "hud-chip", "hud-chip--lap");
            time = UIKit.Label("", "hud-chip");
            topRight.Add(lap);
            topRight.Add(time);
            hud.Add(topRight);

            var bottomRight = UIKit.Div("hud-corner", "hud-corner--bottom-right");
            boost = UIKit.Label("TURBO!", "hud-boost");
            var speedRow = UIKit.Div("row");
            speed = UIKit.Label("0", "hud-speed");
            speedRow.Add(speed);
            speedRow.Add(UIKit.Label("km/h", "hud-speed__unit"));
            bottomRight.Add(boost);
            bottomRight.Add(speedRow);
            hud.Add(bottomRight);

            var bottomLeft = UIKit.Div("hud-corner", "hud-corner--bottom-left");
            pauseHint = UIKit.Label(Loc.T("hud.pauseHint"), "small-text");
            bottomLeft.Add(pauseHint);
            hud.Add(bottomLeft);

            var center = UIKit.Div("hud-center");
            countdown = UIKit.Label("", "hud-countdown");
            message = UIKit.Label("", "hud-message");
            lost = UIKit.Label("", "hud-lost");
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

            if (UIKit.PausePressed() && !resultsShown)
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
            position.text = Loc.Ordinal(player.Position);
            positionTotal.text = "/" + race.Racers.Count;
            lap.text = Loc.T("hud.lap", Mathf.Clamp(player.CompletedLaps + 1, 1, race.Laps), race.Laps);
            time.text = Loc.Time(player.Finished ? player.FinishTime : race.RaceTime);
            speed.text = Mathf.RoundToInt(player.Kart.Speed * 3.6f).ToString();
            boost.EnableInClassList("hidden", !player.Kart.IsBoosting);

            bool counting = race.CurrentState == RaceManager.State.Countdown;
            countdown.EnableInClassList("hidden", !counting);
            if (counting) countdown.text = race.Countdown.ToString();

            // Priority: wrong way warning, then timed messages.
            bool wrongWay = player.WrongWay && !player.Finished;
            if (wrongWay)
            {
                message.text = Loc.T("hud.wrongWay");
                message.AddToClassList("hud-message--warning");
            }
            else message.RemoveFromClassList("hud-message--warning");
            message.EnableInClassList("hidden", !wrongWay && Time.unscaledTime > messageUntil);

            bool showLost = player.NeedsHelp && !player.Finished && !race.IsPaused;
            lost.EnableInClassList("hidden", !showLost);
            if (showLost)
                lost.text = Loc.T("hud.lost") + "\n" + Loc.T("hud.autoReturn", Mathf.CeilToInt(Mathf.Max(0f, player.AutoReturnIn)));

            pauseHint.EnableInClassList("hidden", resultsShown);
        }

        void ShowMessage(string text, float seconds = 1.6f)
        {
            message.text = text;
            messageUntil = Time.unscaledTime + seconds;
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
                ShowMessage(Loc.T("hud.finalLap") + "\n" + Loc.T("hud.lapTime", racer.CompletedLaps, Loc.Time(racer.LastLap)), 2.2f);
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
            var panel = UIKit.Div("panel");
            panel.Add(UIKit.Label(Loc.T("pause.title"), "panel-title"));
            var resume = UIKit.Button(Loc.T("pause.resume"), () => SetPaused(false));
            panel.Add(resume);
            panel.Add(UIKit.Button(Loc.T("pause.restart"), race.Restart, "candy-button--mint"));
            panel.Add(UIKit.Button(Loc.T("pause.checkpoint"), () => { CloseOverlay(); race.PlayerBackToTrack(); }, "candy-button--lavender"));
            panel.Add(UIKit.Button(Loc.T("pause.mainMenu"), race.QuitToMenu, "candy-button--lemon"));
            overlay.Add(panel);
            root.Add(overlay);
            UIKit.FocusLater(resume);
        }

        void CloseOverlay()
        {
            overlay?.RemoveFromHierarchy();
            overlay = null;
        }

        // ------------------------------------------------------------ Results

        void ShowResults()
        {
            CloseOverlay();
            resultsShown = true;
            var player = race.Player;

            overlay = UIKit.Div("screen", "screen--dim");
            var panel = UIKit.Div("panel", "panel--wide");
            panel.Add(UIKit.Label(Loc.T("results.title"), "panel-title"));
            panel.Add(UIKit.Label(Loc.Ordinal(player.Position), "hud-position"));
            if (race.NewRecord) panel.Add(UIKit.Label(Loc.T("results.newRecord"), "results-highlight"));

            resultsRows = UIKit.Div();
            panel.Add(resultsRows);
            panel.Add(UIKit.Label(Loc.T("results.bestLap", Loc.Time(player.BestLap)), "results-highlight"));

            var buttons = UIKit.Div("row");
            var again = UIKit.Button(Loc.T("results.retry"), race.Restart, "candy-button--small");
            buttons.Add(again);
            buttons.Add(UIKit.Button(Loc.T("pause.mainMenu"), race.QuitToMenu, "candy-button--lemon", "candy-button--small"));
            panel.Add(buttons);

            overlay.Add(panel);
            root.Add(overlay);
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
