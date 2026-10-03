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
        KartController kart;
        RaceProgress progress;

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

            float throttle = 0f, steer = 0f;
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
                steer += pad.leftStick.x.ReadValue();
                drift |= pad.rightShoulder.isPressed || pad.buttonWest.isPressed;
                backToTrack |= pad.buttonNorth.wasPressedThisFrame;
            }

            kart.Throttle = Mathf.Clamp(throttle, -1f, 1f);
            kart.Steer = Mathf.Clamp(steer, -1f, 1f);
            kart.DriftHeld = drift;
            if (backToTrack) kart.Respawn();
        }
    }
}
