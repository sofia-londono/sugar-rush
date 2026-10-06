using UnityEngine;
using UnityEngine.InputSystem;

namespace SugarRush
{
    /// <summary>
    /// On-screen driving buttons for phones and tablets (written by RaceUI's touch overlay,
    /// read by PlayerKartInput for the single local player). With "auto-accelerate" the kart
    /// drives forward by itself and the player only steers, brakes and drifts.
    /// </summary>
    public static class TouchDriving
    {
        public static bool Left, Right, Gas, Brake, Drift;
        static bool backToTrack;

        /// <summary>Show the on-screen controls: phones / tablets (web or native).</summary>
        public static bool Enabled => ForceForTesting || Application.isMobilePlatform || (Touchscreen.current != null && Keyboard.current == null);

        /// <summary>Show the touch controls on a desktop too (tests / screenshots).</summary>
        public static bool ForceForTesting;

        public static void RequestBackToTrack() => backToTrack = true;

        /// <summary>Read once per frame by the player's input.</summary>
        public static bool ConsumeBackToTrack()
        {
            bool b = backToTrack;
            backToTrack = false;
            return b;
        }

        public static void Release() => Left = Right = Gas = Brake = Drift = false;
    }
}
