using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    public enum CameraMode { Menu, Follow, Celebrate, Shop }

    /// <summary>
    /// Smooth third-person camera behind the cat. Looks ahead towards obstacles, rises gently during jumps, and adds only
    /// subtle effects (a small landing dip, a slight FOV widening during Speed Boost, a slow orbit at the finish).
    /// No harsh shaking; "Calm camera" (reduce motion) turns the effects off entirely.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        public Camera cam;
        public CameraMode Mode { get; private set; } = CameraMode.Menu;
        public bool ReduceMotion { get; set; }
        public float SpeedBoost { get; set; } // 0..1
        private float landDip, orbit, bump, baseFov = 60f;
        private Vector3 velocity;

        public void SetMode(CameraMode m) { Mode = m; orbit = 0; }
        public void OnLanded() { if (!ReduceMotion) landDip = 0.12f; }
        public void OnBump() { if (!ReduceMotion) bump = 0.25f; }

        public void SnapBehind()
        {
            if (!target) return;
            transform.position = target.position + new Vector3(0, 3.6f, 6.8f);
            transform.LookAt(target.position + new Vector3(0, 1f, -9f));
        }

        private void LateUpdate()
        {
            if (!target || !cam) return;
            float dt = Time.unscaledDeltaTime;
            Vector3 desired; Vector3 look;
            switch (Mode)
            {
                case CameraMode.Menu:
                    orbit += dt * 8f;
                    desired = target.position + Quaternion.Euler(0, 200 + Mathf.Sin(orbit * 0.05f) * 12f, 0) * new Vector3(0, 1.3f, 3.6f);
                    look = target.position + Vector3.up * 0.8f; break;
                case CameraMode.Shop:
                    desired = target.position + new Vector3(0.6f, 1.2f, -3.0f); look = target.position + Vector3.up * 0.75f; break;
                case CameraMode.Celebrate:
                    orbit += dt * 25f;
                    desired = target.position + Quaternion.Euler(0, 180 + orbit, 0) * new Vector3(0, 1.8f, 4.5f);
                    look = target.position + Vector3.up * 0.9f; break;
                default:
                    float h = Mathf.Max(0, target.localPosition.y);
                    float x = target.position.x * 0.55f; // follow lanes partially: keeps the whole road in view
                    desired = new Vector3(x, 3.4f + h * 0.45f - landDip, target.position.z + 6.6f);
                    look = new Vector3(target.position.x * 0.8f, 1.2f + h * 0.3f, target.position.z - 10f); // look ahead
                    break;
            }
            landDip = Mathf.MoveTowards(landDip, 0, dt * 0.6f);
            float smooth = Mode == CameraMode.Follow ? 0.08f : 0.35f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smooth, Mathf.Infinity, dt);
            if (bump > 0 && !ReduceMotion) { transform.position += Random.insideUnitSphere * bump * 0.08f; bump = Mathf.MoveTowards(bump, 0, dt * 1.5f); }
            var rot = Quaternion.LookRotation(look - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-10f * dt));
            float fov = baseFov + (ReduceMotion ? 0 : SpeedBoost * 8f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-4f * dt));
        }
    }
}
