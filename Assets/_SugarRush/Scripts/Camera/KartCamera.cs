using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Third-person chase camera that follows a kart and widens its FOV with speed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class KartCamera : MonoBehaviour
    {
        public KartController target;
        public float distance = 5.5f;
        public float height = 2.2f;
        public float lookHeight = 0.8f;
        public float positionSmoothTime = 0.12f;
        public float rotationSharpness = 10f;
        public float baseFov = 62f;
        public float maxFovBonus = 14f;

        Camera cam;
        Vector3 velocity;
        Vector3 smoothedForward;
        Vector3 smoothPosition; // without shake, so shaking never feeds back into the follow
        float shake;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target) smoothedForward = target.transform.forward;
            smoothPosition = transform.position;
        }

        /// <summary>Shakes the view (metres of jitter, fades out over ~0.6 s). Stronger calls win.</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.deltaTime;
            Transform t = target.transform;

            Vector3 flatForward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f) flatForward = smoothedForward;
            smoothedForward = Vector3.Slerp(smoothedForward, flatForward.normalized, 1f - Mathf.Exp(-6f * dt));

            Vector3 desired = t.position - smoothedForward * distance + Vector3.up * height;
            smoothPosition = Vector3.SmoothDamp(smoothPosition, desired, ref velocity, positionSmoothTime);
            transform.position = smoothPosition;

            Vector3 lookPoint = t.position + Vector3.up * lookHeight;
            Quaternion look = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotationSharpness * dt));

            if (shake > 0.002f)
            {
                float time = Time.time * 32f;
                transform.position += new Vector3(Mathf.PerlinNoise(time, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, time) - 0.5f, Mathf.PerlinNoise(time, 5.1f) - 0.5f) * (2f * shake);
                shake *= Mathf.Exp(-5f * dt);
            }

            float speed01 = Mathf.Clamp01(target.Speed / Mathf.Max(1f, target.MaxSpeed));
            float fov = baseFov + maxFovBonus * speed01 + (target.IsBoosting ? 6f : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-4f * dt));
        }

        public void SnapToTarget()
        {
            if (!target) return;
            smoothedForward = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            transform.position = target.transform.position - smoothedForward * distance + Vector3.up * height;
            smoothPosition = transform.position;
            transform.LookAt(target.transform.position + Vector3.up * lookHeight);
            velocity = Vector3.zero;
        }
    }
}
