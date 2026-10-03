using System;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SugarRush
{
    /// <summary>Small helpers for building the code-driven UI Toolkit screens.</summary>
    public static class UIKit
    {
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

        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.ClearClassList();
            b.AddToClassList("candy-button");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static Button ArrowButton(string text, Action onClick, bool small = false)
        {
            var b = new Button(onClick) { text = text, focusable = false };
            b.ClearClassList();
            b.AddToClassList("arrow-button");
            if (small) b.AddToClassList("arrow-button--small");
            return b;
        }

        public static void FocusLater(Focusable element)
        {
            if (element is VisualElement ve) ve.schedule.Execute(() => element.Focus()).ExecuteLater(1);
        }

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
