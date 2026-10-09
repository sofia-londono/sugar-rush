using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Spins and steers the wheel meshes and leans the body into drifts. Purely cosmetic.
    /// </summary>
    public class KartVisuals : MonoBehaviour
    {
        [System.Serializable]
        public class Wheel
        {
            public Transform steerPivot;
            public Transform spin;
            public bool isFront;
        }

        public KartController kart;
        public Transform model;
        public Wheel[] wheels;
        public float maxSteerAngle = 25f;
        public float driftYaw = 22f;

        float spinAngle;
        float currentYaw;

        void LateUpdate()
        {
            if (!kart) return;
            float dt = Time.deltaTime;

            spinAngle += kart.ForwardSpeed / Mathf.Max(0.05f, kart.wheelRadius) * Mathf.Rad2Deg * dt;
            spinAngle %= 360f;
            float steerAngle = kart.Steer * maxSteerAngle;

            foreach (var w in wheels)
            {
                if (w.steerPivot && w.isFront) w.steerPivot.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
                if (w.spin) w.spin.localRotation = Quaternion.Euler(spinAngle, 0f, 0f);
            }

            if (model)
            {
                float targetYaw = kart.IsDrifting ? kart.DriftDirection * driftYaw : 0f;
                currentYaw = Mathf.Lerp(currentYaw, targetYaw, 1f - Mathf.Exp(-8f * dt));
                // Trick: a full barrel roll (eased) while the trick is in the air.
                float t = kart.TrickProgress;
                float roll = t > 0f ? Mathf.SmoothStep(0f, 360f, t) : 0f;
                model.localRotation = Quaternion.Euler(0f, currentYaw, roll);
            }
        }
    }
}
