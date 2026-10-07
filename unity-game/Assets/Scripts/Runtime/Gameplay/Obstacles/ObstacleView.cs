using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>
    /// An obstacle on the track. Collision is resolved by lane and height (deterministic, fair), not by physics:
    ///   jump          – hit if the cat's feet are below the top
    ///   jump_or_slide – safe when sliding under or jumping over
    ///   high_jump     – like jump, taller
    ///   wall          – needs a double jump (or another lane)
    ///   timed         – falling rocks / moving critter, see subclasses
    /// </summary>
    public class ObstacleView : MonoBehaviour
    {
        public ObstacleDef def;
        public int lane;
        public float planZ;         // metres from the start
        public bool Active { get; protected set; }
        public ObstaclePool pool;
        private Vector3 knockVelocity; private bool knocked;

        public virtual void Spawn(ObstacleDef d, int lane, float planZ, float phase)
        {
            def = d; this.lane = lane; this.planZ = planZ; Active = true; knocked = false;
            transform.localPosition = new Vector3(lane < 0 ? 0 : (lane - 1) * LevelPlanner.LaneWidth, 0, -planZ - def.depth * 0.5f);
            transform.localRotation = Quaternion.identity;
            gameObject.SetActive(true);
        }

        /// <summary>Called every frame with the cat's distance from the start.</summary>
        public virtual void Tick(float dt, float catDistance)
        {
            if (!knocked) return;
            knockVelocity.y -= 25f * dt;
            transform.localPosition += knockVelocity * dt;
            transform.Rotate(400f * dt, 0, 0);
            if (transform.localPosition.y < -6f) Release();
        }

        public virtual float CurrentX => transform.localPosition.x;
        public virtual float HalfDepth => def.depth * 0.5f;
        public virtual float CenterDistance => planZ + def.depth * 0.5f;

        public virtual bool Hits(CatController cat)
        {
            const float margin = 0.15f; // forgiving: the feet may brush the top
            switch (def.clearance)
            {
                case "jump": case "high_jump": return cat.Y < def.height - margin;
                case "jump_or_slide": return !cat.Sliding && cat.Y < def.topHeight - margin;
                case "wall": return cat.Y < def.height - 0.25f;
                default: return cat.Y < def.height - margin;
            }
        }

        public void KnockAway()
        {
            Active = false; knocked = true;
            knockVelocity = new Vector3(Random.Range(-4f, 4f), 7f, 5f);
        }

        public virtual void Release()
        {
            Active = false; knocked = false; gameObject.SetActive(false);
            if (pool != null) pool.Return(this); else Destroy(gameObject);
        }
    }

    /// <summary>
    /// Falling rocks (or coconuts at the beach): shakes with dust and a warning sound when the cat approaches, a shadow shows
    /// exactly where it will land, then it drops well before the cat arrives and stays as a rock to jump or dodge.
    /// </summary>
    public class FallingRockView : ObstacleView
    {
        public Transform rock, shadow; public ParticleSystem dust;
        private bool warned, dropped; private float fallT, shakeT;
        private const float DropHeight = 7f, WarnAt = 42f, DropAt = 26f;

        public override void Spawn(ObstacleDef d, int lane, float planZ, float phase)
        {
            base.Spawn(d, lane, planZ, phase);
            warned = dropped = false; fallT = shakeT = 0;
            rock.localPosition = new Vector3(0, DropHeight, 0); rock.localRotation = Quaternion.identity;
            if (shadow) shadow.localScale = new Vector3(0.5f, 1, 0.5f);
        }

        public override void Tick(float dt, float catDistance)
        {
            base.Tick(dt, catDistance);
            if (!Active) return;
            float ahead = planZ - catDistance;
            if (!warned && ahead < WarnAt)
            {
                warned = true;
                if (dust) dust.Play();
                GameEvents.RaiseObstacleWarning(transform.position);
            }
            if (warned && !dropped)
            {
                shakeT += dt;
                rock.localPosition = new Vector3(Mathf.Sin(shakeT * 55f) * 0.07f, DropHeight, Mathf.Cos(shakeT * 47f) * 0.05f);
                if (shadow) shadow.localScale = Vector3.Lerp(shadow.localScale, new Vector3(1.4f, 1, 1.4f), dt * 2f);
                if (ahead < DropAt) dropped = true;
            }
            if (dropped && fallT < 1f)
            {
                fallT = Mathf.Min(1f, fallT + dt / 0.55f);
                float y = Mathf.Lerp(DropHeight, 0.5f, fallT * fallT);
                rock.localPosition = new Vector3(0, y, 0);
                rock.Rotate(300f * dt, 0, 120f * dt);
                if (fallT >= 1f) { if (dust) dust.Play(); GameEvents.RaiseBumped(transform.position, true); }
            }
        }

        public override bool Hits(CatController cat)
        {
            float rockBottom = rock.localPosition.y - 0.5f;
            if (rockBottom > cat.Top) return false; // still above the cat's head (it never drops this late, but be safe)
            return cat.Y < def.height - 0.15f;
        }
    }

    /// <summary>A friendly critter walking side to side on a dotted path with a steady, predictable rhythm. Jump over it or wait and dodge.</summary>
    public class MovingCritterView : ObstacleView
    {
        public Transform body;
        private float phase, t;
        public const float Period = 3.4f;

        public override void Spawn(ObstacleDef d, int lane, float planZ, float phase)
        {
            base.Spawn(d, 1, planZ, phase); // the row is centred; the critter walks across all lanes
            this.phase = phase; t = 0;
        }

        public override float CurrentX => body.localPosition.x;

        public override void Tick(float dt, float catDistance)
        {
            base.Tick(dt, catDistance);
            if (!Active) return;
            t += dt;
            float s = Mathf.Sin((t / Period + phase) * Mathf.PI * 2);
            body.localPosition = new Vector3(s * LevelPlanner.LaneWidth, Mathf.Abs(Mathf.Sin(t * 9f)) * 0.08f, 0);
            float dir = Mathf.Cos((t / Period + phase) * Mathf.PI * 2);
            body.localRotation = Quaternion.Euler(0, dir >= 0 ? 90 : -90, Mathf.Sin(t * 12f) * 6f);
        }

        public override bool Hits(CatController cat) => Mathf.Abs(CurrentX + transform.localPosition.x - cat.X) < 0.9f && cat.Y < def.height - 0.1f;
    }

    /// <summary>Visual only: the run controller removes the ground under the cat inside the gap. Cracks and broken boards warn early.</summary>
    public class BrokenBridgeView : ObstacleView
    {
        public override bool Hits(CatController cat) => false;
    }
}
