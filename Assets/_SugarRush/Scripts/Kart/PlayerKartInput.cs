using UnityEngine;
using UnityEngine.InputSystem;

namespace SugarRush
{
    /// <summary>
    /// Keyboard / gamepad driver for a KartController.
    /// WASD or arrows to drive, Space/Shift to drift, R (gamepad Y) to get back on track.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class PlayerKartInput : MonoBehaviour
    {
        [Header("Steering feel")]
        [Tooltip("How fast keyboard steering builds up to full lock (per second).")]
        public float steerRise = 4f;
        [Tooltip("How fast steering returns to centre when the key is released (per second).")]
        public float steerFall = 8f;
        [Tooltip("Gamepad stick response: 1 = linear, 2 = gentle near the centre.")]
        public float stickCurve = 1.8f;

        KartController kart;
        RaceProgress progress;
        float smoothedSteer;

        void Awake()
        {
            kart = GetComponent<KartController>();
            progress = GetComponent<RaceProgress>();
        }

        void OnDisable()
        {
            if (!kart) return;
            kart.Throttle = 0f; kart.Steer = 0f; kart.DriftHeld = false;
        }

        void Update()
        {
            var manager = RaceManager.Instance;
            if (manager && (manager.IsPaused || !manager.CanDrive(progress)))
            {
                kart.Throttle = 0f; kart.Steer = 0f; kart.DriftHeld = false;
                return;
            }

            float throttle = 0f, steer = 0f, stick = 0f;
            bool drift = false, backToTrack = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                drift |= kb.spaceKey.isPressed || kb.leftShiftKey.isPressed;
                backToTrack |= kb.rKey.wasPressedThisFrame;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                throttle += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (pad.buttonSouth.isPressed) throttle += 1f;
                float x = pad.leftStick.x.ReadValue();
                stick = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), stickCurve);
                drift |= pad.rightShoulder.isPressed || pad.buttonWest.isPressed;
                backToTrack |= pad.buttonNorth.wasPressedThisFrame;
            }

            // Keys are all-or-nothing, so ease them in: a tap gives a small correction, holding
            // gives full lock. Releasing (or reversing) returns faster than it builds up.
            bool releasing = Mathf.Abs(steer) < 0.01f;
            bool reversing = Mathf.Abs(smoothedSteer) > 0.01f && Mathf.Sign(steer) != Mathf.Sign(smoothedSteer);
            float rate = releasing || reversing ? steerFall : steerRise;
            smoothedSteer = Mathf.MoveTowards(smoothedSteer, Mathf.Clamp(steer, -1f, 1f), rate * Time.deltaTime);
            float finalSteer = Mathf.Abs(stick) > Mathf.Abs(smoothedSteer) ? stick : smoothedSteer;

            kart.Throttle = Mathf.Clamp(throttle, -1f, 1f);
            kart.Steer = Mathf.Clamp(finalSteer, -1f, 1f);
            kart.DriftHeld = drift;
            if (backToTrack) kart.Respawn();
        }
    }
}
