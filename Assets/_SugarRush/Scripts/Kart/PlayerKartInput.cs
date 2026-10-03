using UnityEngine;
using UnityEngine.InputSystem;

namespace SugarRush
{
    /// <summary>
    /// Keyboard / gamepad driver for a KartController.
    /// WASD or arrows to drive, Space/Shift to drift, R to respawn.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class PlayerKartInput : MonoBehaviour
    {
        KartController kart;

        void Awake() => kart = GetComponent<KartController>();

        void Update()
        {
            float throttle = 0f, steer = 0f;
            bool drift = false, reset = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                drift |= kb.spaceKey.isPressed || kb.leftShiftKey.isPressed;
                reset |= kb.rKey.wasPressedThisFrame;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                throttle += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                steer += pad.leftStick.x.ReadValue();
                drift |= pad.rightShoulder.isPressed || pad.buttonWest.isPressed;
                reset |= pad.selectButton.wasPressedThisFrame;
            }

            kart.Throttle = Mathf.Clamp(throttle, -1f, 1f);
            kart.Steer = Mathf.Clamp(steer, -1f, 1f);
            kart.DriftHeld = drift;
            if (reset) kart.Respawn();
        }
    }
}
