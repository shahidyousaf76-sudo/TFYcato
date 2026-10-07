using System;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>
    /// The cat's movement: three lanes, jump, double jump, long jump, slide, landing, falling into a gap and climbing out,
    /// hit reaction and celebration. Physics is tuned to feel fun and forgiving rather than realistic:
    /// coyote time (jump just after leaving the ground), input buffering (tap slightly early still jumps), soft lane changes.
    /// Pure local simulation — never waits for the network.
    /// </summary>
    public class CatController : MonoBehaviour
    {
        // Tuning (also used by the level planner's fairness rules — keep in sync with ObstacleDef heights).
        public const float JumpVelocity = 11.5f, DoubleJumpVelocity = 10.2f, Gravity = 30f, FastFall = -24f;
        public const float LongJumpVelocity = 10.5f, LongJumpGravityUp = 15f, LongJumpGravityDown = 22f;
        public const float SlideSeconds = 0.7f, CoyoteSeconds = 0.12f, JumpBufferSeconds = 0.16f;
        public const float HitStunSeconds = 0.35f, InvulnerableSeconds = 1.5f, LaneSharpness = 16f;

        public CatAnimator anim;
        public int Lane { get; private set; } = 1;
        public float X => transform.localPosition.x;
        public float Y { get; private set; }
        public bool Grounded { get; private set; } = true;
        public bool Sliding => slideTimer > 0 && Grounded;
        public bool InGap { get; private set; }
        public bool Invulnerable => invulnTimer > 0;
        public bool Running { get; private set; }
        public float ForwardSpeed { get; set; }
        public float JumpMultiplier { get; set; } = 1f;   // Super Jump power-up
        public bool Boosting { get; set; }                // Speed Boost power-up (fast run)
        public bool LongJumpZone { get; set; }             // any jump here becomes a long jump (bridges)
        public int Jumps { get; private set; }
        public event Action FellIntoGap;

        private float vy, gravity = Gravity, slideTimer, hitTimer, invulnTimer, coyote, jumpBuffer, lean, landTimer, climbTimer;
        private int jumpsUsed;
        private bool longJumping, falling;

        public void ResetTo(float x, int lane)
        {
            Lane = lane; Y = 0; vy = 0; Grounded = true; InGap = false; falling = false; slideTimer = hitTimer = invulnTimer = climbTimer = 0; jumpsUsed = 0; longJumping = false;
            JumpMultiplier = 1; Boosting = false; LongJumpZone = false; Jumps = 0;
            transform.localPosition = new Vector3((lane - 1) * LevelPlanner.LaneWidth, 0, transform.localPosition.z);
            anim.StopBlink();
        }

        public void BeginRun() { Running = true; anim.Play(CatAnim.Run); }
        public void StopRun(CatAnim pose) { Running = false; anim.Play(pose); anim.RunSpeed = 0; }
        public void SetMenuPose(CatAnim pose) { Running = false; anim.Play(pose); }

        // ------------------------------------------------------------ input
        public void Tap() { if (!CanAct) return; if (!TryJump(false)) jumpBuffer = JumpBufferSeconds; }
        public void SwipeUp() { if (!CanAct) return; if (!TryJump(true)) jumpBuffer = JumpBufferSeconds; }
        public void SwipeDown()
        {
            if (!CanAct) return;
            if (!Grounded) { vy = Mathf.Min(vy, FastFall); longJumping = false; } // drop quickly, slide on landing
            slideTimer = SlideSeconds; if (Grounded) anim.Play(CatAnim.Sliding);
            GameEvents.RaiseSlid();
        }
        public void SwipeLeft() => ChangeLane(-1);
        public void SwipeRight() => ChangeLane(1);
        private bool CanAct => Running && hitTimer <= 0 && !falling && climbTimer <= 0;

        private void ChangeLane(int dir)
        {
            if (!CanAct) return;
            int next = Mathf.Clamp(Lane + dir, 0, 2);
            if (next == Lane) return;
            Lane = next; lean = dir; GameEvents.RaiseLaneChanged();
        }

        private bool TryJump(bool swipeUp)
        {
            bool onGround = Grounded || coyote > 0;
            if (onGround)
            {
                bool longJump = swipeUp || LongJumpZone;
                longJumping = longJump;
                vy = (longJump ? LongJumpVelocity : JumpVelocity) * Mathf.Sqrt(JumpMultiplier);
                gravity = longJump ? LongJumpGravityUp : Gravity;
                Grounded = false; coyote = 0; jumpsUsed = 1; slideTimer = 0; Jumps++;
                anim.Play(longJump ? CatAnim.LongJump : CatAnim.Jump);
                if (longJump) GameEvents.RaiseLongJumped(); else GameEvents.RaiseJumped();
                return true;
            }
            if (jumpsUsed < 2)
            {
                vy = DoubleJumpVelocity * Mathf.Sqrt(JumpMultiplier); gravity = longJumping ? LongJumpGravityDown : Gravity;
                jumpsUsed = 2; Jumps++;
                anim.Play(CatAnim.DoubleJump); GameEvents.RaiseDoubleJumped();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ hits, gaps
        /// <summary>Returns true if the bump costs a heart (false = shield / boost / invulnerable absorbed it).</summary>
        public bool TakeHit(bool shield, bool invincible)
        {
            if (invincible || Invulnerable) return false;
            if (shield) { invulnTimer = 0.6f; return false; }
            hitTimer = HitStunSeconds; invulnTimer = InvulnerableSeconds;
            anim.Play(CatAnim.Hit); anim.Blink = true;
            return true;
        }

        /// <summary>The run controller tells the cat whether there is ground under it (false over a broken bridge gap).</summary>
        public void SetGround(bool hasGround)
        {
            if (hasGround || !Grounded || falling || !Running) { noGroundTime = 0; return; }
            // Coyote time at the edge: a tap in the first moments over the gap still jumps.
            noGroundTime += Time.deltaTime;
            if (noGroundTime > CoyoteSeconds) { falling = true; InGap = true; Grounded = false; vy = -2f; gravity = Gravity; anim.Play(CatAnim.Falling); noGroundTime = 0; }
        }
        private float noGroundTime;

        /// <summary>After falling: pop out at the far side of the gap and climb up (friendly, no endless fall).</summary>
        public void ClimbOut()
        {
            falling = false; InGap = false; Y = 0; vy = 0; Grounded = true; jumpsUsed = 0; climbTimer = 0.55f;
            invulnTimer = InvulnerableSeconds; anim.Blink = true; anim.Play(CatAnim.Climbing);
        }

        // ------------------------------------------------------------ simulation
        private void Update()
        {
            float dt = Time.deltaTime;
            if (invulnTimer > 0 && (invulnTimer -= dt) <= 0) anim.StopBlink();
            anim.RunSpeed = Running ? ForwardSpeed : 0;
            if (!Running) return;

            if (jumpBuffer > 0) { jumpBuffer -= dt; if (Grounded && CanAct && TryJump(false)) jumpBuffer = 0; }
            if (coyote > 0) coyote -= dt;
            if (hitTimer > 0 && (hitTimer -= dt) <= 0) anim.Play(Grounded ? CatAnim.Run : CatAnim.Falling);
            if (climbTimer > 0 && (climbTimer -= dt) <= 0) anim.Play(CatAnim.Run);

            if (falling)
            {
                vy -= Gravity * dt; Y += vy * dt;
                if (Y < -2.5f) { falling = false; FellIntoGap?.Invoke(); }
            }
            else if (!Grounded)
            {
                if (longJumping && vy < 0) gravity = LongJumpGravityDown;
                vy -= gravity * dt;
                Y += vy * dt;
                if (vy < 0 && (anim.State == CatAnim.Jump || (anim.State == CatAnim.DoubleJump && Y < 1.2f))) anim.Play(CatAnim.Falling);
                if (Y <= 0f)
                {
                    Y = 0; vy = 0; Grounded = true; jumpsUsed = 0; longJumping = false; gravity = Gravity; landTimer = 0.15f;
                    GameEvents.RaiseLanded();
                    anim.Play(slideTimer > 0 ? CatAnim.Sliding : CatAnim.Landing);
                }
            }

            if (Grounded)
            {
                if (landTimer > 0 && (landTimer -= dt) <= 0 && anim.State == CatAnim.Landing) anim.Play(Boosting ? CatAnim.FastRun : CatAnim.Run);
                if (slideTimer > 0 && (slideTimer -= dt) <= 0 && anim.State == CatAnim.Sliding) anim.Play(Boosting ? CatAnim.FastRun : CatAnim.Run);
                if (anim.State == CatAnim.Run && Boosting) anim.Play(CatAnim.FastRun);
                if (anim.State == CatAnim.FastRun && !Boosting) anim.Play(CatAnim.Run);
                coyote = CoyoteSeconds;
            }

            var p = transform.localPosition;
            p.x = Mathf.Lerp(p.x, (Lane - 1) * LevelPlanner.LaneWidth, 1f - Mathf.Exp(-LaneSharpness * dt));
            p.y = Y;
            transform.localPosition = p;
            lean = Mathf.MoveTowards(lean, 0, dt * 4f);
            anim.Lean = lean;
        }

        /// <summary>Top of the cat's body for overhead obstacles (lower while sliding).</summary>
        public float Top => Sliding ? 0.6f : Y + 1.15f;
    }
}
