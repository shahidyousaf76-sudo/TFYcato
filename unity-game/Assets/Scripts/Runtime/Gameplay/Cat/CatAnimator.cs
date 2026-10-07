using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    public enum CatAnim { Idle, Walk, Run, FastRun, Jump, DoubleJump, LongJump, Falling, Landing, Sliding, Climbing, Hit, Victory, Dancing, Sleeping, Happy }

    /// <summary>
    /// Procedural animation for the cat: every pose is computed from time and blended towards smoothly
    /// (frame-rate independent), so transitions never snap. If a rigged model with an Animator is attached instead,
    /// the same states drive its "State" integer parameter.
    /// </summary>
    public class CatAnimator : MonoBehaviour
    {
        public CatRig rig;
        public Animator animator; // optional rigged model
        public CatAnim State { get; private set; } = CatAnim.Idle;
        public float RunSpeed { get; set; }
        public float Lean { get; set; }           // -1..1 while changing lanes
        public bool Blink { get; set; }           // invulnerable blinking
        private float stateTime, blinkTimer = 2f, earTimer = 3f, flip;
        private static readonly int StateHash = Animator.StringToHash("State");
        private static readonly int SpeedHash = Animator.StringToHash("RunSpeed");

        public void Play(CatAnim s)
        {
            if (State == s) return;
            State = s; stateTime = 0; if (s == CatAnim.DoubleJump) flip = 0;
            if (animator) animator.SetInteger(StateHash, (int)s);
        }

        private void Update()
        {
            if (!rig) return;
            float dt = Time.deltaTime, t = Time.time; stateTime += dt;
            if (animator) animator.SetFloat(SpeedHash, RunSpeed / 10f);

            // Targets (degrees / local offsets), then smooth blend.
            float bodyY = 0.55f, pitch = 0, roll = -Lean * 12f, yaw = 0, squashY = 1, stretchZ = 1, headPitch = 0, headYaw = 0, tailUp = 30, tailWag = 0;
            float[] leg = new float[4];
            float gait = RunSpeed * 1.25f;

            switch (State)
            {
                case CatAnim.Idle:
                    squashY = 1 + Mathf.Sin(t * 2.2f) * 0.02f; headYaw = Mathf.Sin(t * 0.5f) * 20f; tailWag = Mathf.Sin(t * 1.6f) * 25f; tailUp = 45; break;
                case CatAnim.Walk:
                    for (int i = 0; i < 4; i++) leg[i] = Mathf.Sin(t * 6f + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 22f;
                    bodyY += Mathf.Abs(Mathf.Sin(t * 6f)) * 0.03f; tailWag = Mathf.Sin(t * 3f) * 15f; break;
                case CatAnim.Run:
                case CatAnim.FastRun:
                    bool fast = State == CatAnim.FastRun;
                    float f = t * Mathf.Max(8f, gait) * (fast ? 1.3f : 1f);
                    leg[0] = Mathf.Sin(f) * 45f; leg[1] = Mathf.Sin(f + 0.5f) * 45f; leg[2] = Mathf.Sin(f + Mathf.PI) * 45f; leg[3] = Mathf.Sin(f + Mathf.PI + 0.5f) * 45f;
                    bodyY += Mathf.Abs(Mathf.Sin(f)) * 0.08f; pitch = Mathf.Sin(f * 2) * 3f + (fast ? 10f : 4f);
                    stretchZ = 1 + Mathf.Sin(f) * 0.04f; tailUp = fast ? 75 : 55; tailWag = Mathf.Sin(f * 0.5f) * 10f; headPitch = -pitch * 0.6f; break;
                case CatAnim.Jump:
                    for (int i = 0; i < 4; i++) leg[i] = i < 2 ? -50 : 50; pitch = -12; stretchZ = 1.08f; tailUp = 80; break;
                case CatAnim.DoubleJump:
                    flip = Mathf.Min(360f, flip + dt * 800f); pitch = -flip; squashY = 0.9f;
                    for (int i = 0; i < 4; i++) leg[i] = 70; tailUp = 90; break;
                case CatAnim.LongJump:
                    leg[0] = leg[1] = -75; leg[2] = leg[3] = 75; stretchZ = 1.25f; squashY = 0.9f; pitch = -6; tailUp = 5; break;
                case CatAnim.Falling:
                    for (int i = 0; i < 4; i++) leg[i] = (i % 2 == 0 ? 1 : -1) * 35 + Mathf.Sin(t * 20 + i) * 10; pitch = 15; tailUp = 110; headPitch = -15; break;
                case CatAnim.Landing:
                    float k = Mathf.Clamp01(stateTime / 0.18f); squashY = Mathf.Lerp(0.72f, 1f, k); stretchZ = Mathf.Lerp(1.15f, 1f, k); bodyY -= (1 - k) * 0.12f; break;
                case CatAnim.Sliding:
                    bodyY = 0.28f; squashY = 0.7f; stretchZ = 1.2f; pitch = 4; headPitch = 10;
                    leg[0] = leg[1] = -80; leg[2] = leg[3] = 70; tailUp = 0; break;
                case CatAnim.Climbing:
                    pitch = -55; float c = t * 10f;
                    leg[0] = -60 + Mathf.Sin(c) * 40; leg[1] = -60 + Mathf.Sin(c + Mathf.PI) * 40; leg[2] = Mathf.Sin(c + Mathf.PI) * 30; leg[3] = Mathf.Sin(c) * 30; tailUp = 20; break;
                case CatAnim.Hit:
                    roll = Mathf.Sin(stateTime * 40f) * 12f * Mathf.Max(0, 1 - stateTime * 2.5f); pitch = 12; headPitch = -20; squashY = 0.92f; tailUp = 100; break;
                case CatAnim.Victory:
                    bodyY += Mathf.Abs(Mathf.Sin(t * 5f)) * 0.5f; leg[0] = leg[1] = -120 + Mathf.Sin(t * 10) * 20; pitch = -25; tailWag = Mathf.Sin(t * 8f) * 35f; tailUp = 70; break;
                case CatAnim.Dancing:
                    roll = Mathf.Sin(t * 6f) * 18f; yaw = Mathf.Sin(t * 3f) * 40f; bodyY += Mathf.Abs(Mathf.Sin(t * 6f)) * 0.12f;
                    leg[0] = Mathf.Sin(t * 6f) * 60 - 40; leg[1] = -Mathf.Sin(t * 6f) * 60 - 40; tailWag = Mathf.Sin(t * 12f) * 40f; break;
                case CatAnim.Sleeping:
                    bodyY = 0.25f; squashY = 0.75f; stretchZ = 1.1f; headPitch = 25; roll = 8; tailUp = -10; tailWag = 60;
                    for (int i = 0; i < 4; i++) leg[i] = i < 2 ? -85 : 85; break;
                case CatAnim.Happy:
                    bodyY += Mathf.Abs(Mathf.Sin(t * 9f)) * 0.18f; tailWag = Mathf.Sin(t * 14f) * 45f; tailUp = 70; headYaw = Mathf.Sin(t * 4f) * 12f; break;
            }

            float s = 1f - Mathf.Exp(-14f * dt); // blend speed
            var bp = rig.bodyPivot;
            var lp = bp.localPosition; lp.y = Mathf.Lerp(lp.y, bodyY, s); bp.localPosition = lp;
            var targetRot = Quaternion.Euler(pitch, yaw, roll);
            bp.localRotation = State == CatAnim.DoubleJump ? targetRot : Quaternion.Slerp(bp.localRotation, targetRot, s); // the flip is driven directly
            bp.localScale = Vector3.Lerp(bp.localScale, new Vector3(1f / Mathf.Sqrt(squashY * stretchZ), squashY, stretchZ), s);
            rig.head.localRotation = Quaternion.Slerp(rig.head.localRotation, Quaternion.Euler(headPitch, headYaw, 0), s);
            for (int i = 0; i < 4; i++) if (rig.legs[i]) rig.legs[i].localRotation = Quaternion.Slerp(rig.legs[i].localRotation, Quaternion.Euler(leg[i], 0, 0), s);
            for (int i = 0; i < rig.tail.Count; i++)
                rig.tail[i].localRotation = Quaternion.Slerp(rig.tail[i].localRotation, Quaternion.Euler(i == 0 ? tailUp : 12f, tailWag * (0.3f + i * 0.25f), 0), s); // tip wags more than the base

            // Blinking eyes (closed while sleeping) and ear twitches.
            blinkTimer -= dt; earTimer -= dt;
            bool closed = State == CatAnim.Sleeping || (blinkTimer < 0.12f && blinkTimer > 0f);
            if (blinkTimer <= 0) blinkTimer = Random.Range(2.5f, 5f);
            float eyeY = closed ? 0.1f : 1f;
            rig.eyeL.localScale = new Vector3(1, Mathf.Lerp(rig.eyeL.localScale.y, eyeY, 0.5f), 1); rig.eyeR.localScale = rig.eyeL.localScale;
            float twitch = earTimer < 0.15f ? 15f : 0f; if (earTimer <= 0) earTimer = Random.Range(3f, 7f);
            rig.earL.localRotation = Quaternion.Euler(-twitch, 0, 18f); rig.earR.localRotation = Quaternion.Euler(0, 0, -18f);

            if (Blink) rig.SetVisible(Mathf.FloorToInt(Time.time * 12f) % 2 == 0);
        }

        public void StopBlink() { Blink = false; if (rig) rig.SetVisible(true); }
    }
}
