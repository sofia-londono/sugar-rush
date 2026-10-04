using System;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SugarRush
{
    /// <summary>
    /// Helpers for building the code-driven candy UI: elements, screen transitions, scene fades
    /// and the shared keyboard / gamepad shortcuts.
    /// </summary>
    public static class UIKit
    {
        const long EnterDelayMs = 30, LeaveMs = 220, FadeMs = 380;
        const string BackTag = "back";

        /// <summary>True while the UI moves focus by itself, so it doesn't play the "move" sound.</summary>
        static bool silentFocus;

        public static VisualElement Div(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Label(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static CandyTitle Title(string text, CandyTone tone, string size = "candy-title--lg") => new(text, tone, size);

        /// <summary>
        /// Pill button with a sugar-glass shine and candy-cane stripes that slide across while
        /// it is hovered or focused; USS gives it the elastic bounce.
        /// </summary>
        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = "" };
            b.clicked += () => { if (Equals(b.userData, BackTag)) AudioHub.UIBack(); else AudioHub.UIClick(); };
            b.ClearClassList();
            b.AddToClassList("candy-button");
            foreach (var c in classes) b.AddToClassList(c);

            var stripes = new CandyStripes();
            stripes.AddToClassList("candy-button__stripes");
            b.Add(stripes);
            var gloss = Div("candy-button__gloss");
            gloss.pickingMode = PickingMode.Ignore;
            b.Add(gloss);
            var label = Label(text, "candy-button__label");
            label.pickingMode = PickingMode.Ignore;
            b.Add(label);

            bool hovered = false, focused = false;
            void Lit() => stripes.Animate = hovered || focused;
            b.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; Lit(); AudioHub.UIMove(); });
            b.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; Lit(); });
            b.RegisterCallback<FocusInEvent>(_ => { focused = true; Lit(); if (!silentFocus && !hovered) AudioHub.UIMove(); });
            b.RegisterCallback<FocusOutEvent>(_ => { focused = false; Lit(); });
            return b;
        }

        /// <summary>Candy button that plays the "back" sound instead of the click.</summary>
        public static Button BackButton(string text, Action onClick, params string[] classes)
        {
            var b = Button(text, onClick, classes);
            b.userData = BackTag;
            return b;
        }

        public static string ButtonText(Button b) => b.Q<Label>(className: "candy-button__label")?.text ?? b.text;

        /// <summary>Round peppermint arrow that spins a little when hovered.</summary>
        public static Button ArrowButton(string text, Action onClick, bool small = false)
        {
            var b = new Button(onClick) { text = "", focusable = false };
            b.clicked += AudioHub.UIMove;
            b.ClearClassList();
            b.AddToClassList("peppermint-button");
            if (small) b.AddToClassList("peppermint-button--small");
            b.Add(new PeppermintDisc(CandyPalette.Pink));
            b.Add(new ChevronGlyph(text == "›", CandyPalette.PinkDark));
            return b;
        }

        /// <summary>Small glossy candy pill holding a HUD value. Returns the label to update.</summary>
        public static Label Chip(VisualElement parent, string text, params string[] classes)
        {
            var chip = Div("candy-chip");
            foreach (var c in classes) chip.AddToClassList(c);
            var gloss = Div("candy-chip__gloss");
            chip.Add(gloss);
            var label = Label(text, "candy-chip__label");
            chip.Add(label);
            parent.Add(chip);
            return label;
        }

        // ------------------------------------------------------------ Transitions

        /// <summary>
        /// Replaces the current screen with a new one: the old screen fades and grows away while
        /// the new one pops in (see .screen--entering / .screen--leaving in the USS).
        /// </summary>
        public static void ShowScreen(VisualElement root, VisualElement screen)
        {
            foreach (var old in root.Query<VisualElement>(className: "screen").ToList())
            {
                if (old.parent != root || old.ClassListContains("screen--leaving")) continue;
                Leave(old);
            }
            Enter(root, screen);
        }

        public static void Enter(VisualElement root, VisualElement screen)
        {
            screen.AddToClassList("screen--entering");
            root.Add(screen);
            screen.schedule.Execute(() => screen.RemoveFromClassList("screen--entering")).ExecuteLater(EnterDelayMs);
        }

        public static void Leave(VisualElement screen)
        {
            if (screen == null || screen.parent == null) return;
            screen.AddToClassList("screen--leaving");
            screen.pickingMode = PickingMode.Ignore;
            screen.schedule.Execute(screen.RemoveFromHierarchy).ExecuteLater(LeaveMs);
        }

        /// <summary>Starts the scene behind a pastel curtain that dissolves away.</summary>
        public static void FadeIn(VisualElement root)
        {
            var fader = Div("fader");
            fader.pickingMode = PickingMode.Ignore;
            fader.Add(new SprinkleRain(30));
            root.Add(fader);
            fader.schedule.Execute(() => fader.AddToClassList("fader--clear")).ExecuteLater(EnterDelayMs);
            fader.schedule.Execute(fader.RemoveFromHierarchy).ExecuteLater(FadeMs + 200);
        }

        /// <summary>Covers the screen with the pastel curtain, then runs the action (e.g. load a scene).</summary>
        public static void FadeOut(VisualElement root, Action then)
        {
            var fader = Div("fader", "fader--clear");
            root.Add(fader);
            fader.Add(new SprinkleRain(30));
            fader.schedule.Execute(() => fader.RemoveFromClassList("fader--clear")).ExecuteLater(EnterDelayMs);
            fader.schedule.Execute(then).ExecuteLater(FadeMs + 60);
        }

        /// <summary>Restarts the "pop" animation (scale bounce) on an element.</summary>
        public static void Pop(VisualElement e)
        {
            e.AddToClassList("pop");
            e.schedule.Execute(() => e.RemoveFromClassList("pop")).ExecuteLater(EnterDelayMs);
        }

        public static void FocusLater(Focusable element)
        {
            if (element is VisualElement ve)
                ve.schedule.Execute(() => { silentFocus = true; element.Focus(); silentFocus = false; }).ExecuteLater(EnterDelayMs + 20);
        }

        // ------------------------------------------------------------ Input shortcuts

        public static bool BackPressed() =>
            (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);

        public static bool PausePressed() =>
            (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.startButton.wasPressedThisFrame ?? false);

        /// <summary>-1 / +1 when left / right was pressed this frame on keyboard or gamepad.</summary>
        public static int HorizontalPressed()
        {
            int dir = 0;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dir--;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dir++;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) dir--;
                if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) dir++;
            }
            return Math.Sign(dir);
        }
    }
}
