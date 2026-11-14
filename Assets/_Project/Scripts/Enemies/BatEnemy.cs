using UnityEngine;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Bat (the swarm enemy of GDD 8: many small ones): circles the target at a distance, changing direction now and then,
    /// and from time to time flashes and swoops through it in a straight line, hitting once. Weak and fast, comes in groups.
    /// </summary>
    public class BatEnemy : EnemyBehaviour
    {
        private enum State
        {
            Circle,
            Windup,
            Swoop,
        }

        [Header("Circling")]
        [SerializeField]
        [Min(0f)]
        private float circleRadius = 4f;

        [Tooltip("How strongly the bat keeps its distance on the circle.")]
        [SerializeField]
        [Range(0f, 2f)]
        private float radiusCorrection = 0.8f;

        [SerializeField]
        private Vector2 directionChangeInterval = new Vector2(1.5f, 3.5f);

        [Header("Swoop")]
        [SerializeField]
        private Vector2 attackInterval = new Vector2(1.8f, 3.5f);

        [SerializeField]
        [Min(0f)]
        private float windup = 0.35f;

        [SerializeField]
        [Min(0f)]
        private float swoopDistance = 7f;

        [SerializeField]
        [Min(0.01f)]
        private float swoopDuration = 0.35f;

        [SerializeField]
        [Min(0f)]
        private float hitRadius = 0.6f;

        [SerializeField]
        [Min(1)]
        private int damage = 5;

        [SerializeField]
        [Min(0f)]
        private float knockbackValue = 0.8f;

        [SerializeField]
        private Color telegraphColor = new Color(1f, 0.2f, 0.5f);

        private State state = State.Circle;
        private float stateEndTime;
        private float nextAttackTime;
        private float nextDirectionChange;
        private int circleSide = 1;
        private Vector3 swoopDirection;
        private bool swoopHit;

        private void Start()
        {
            circleSide = Random.value < 0.5f ? -1 : 1;
            nextAttackTime = Time.time + Random.Range(attackInterval.x, attackInterval.y);
        }

        private void Update()
        {
            if (CanAct == false)
            {
                if (state != State.Circle)
                    EndSwoop();
                return;
            }

            switch (state)
            {
                case State.Circle:
                    UpdateCircle();
                    break;

                case State.Windup:
                    Face(OffsetToTarget(), 2f);
                    if (Time.time >= stateEndTime)
                        StartSwoop();
                    break;

                case State.Swoop:
                    UpdateSwoop();
                    break;
            }
        }

        private void UpdateCircle()
        {
            var offset = OffsetToTarget();
            float distance = offset.magnitude;
            if (distance > DetectRange)
                return;

            var toTarget = distance > 0.01f ? offset / distance : TorusObject.Facing;
            if (Time.time >= nextAttackTime && distance <= circleRadius * 1.5f)
            {
                EnterWindup();
                return;
            }

            if (Time.time >= nextDirectionChange)
            {
                nextDirectionChange = Time.time + Random.Range(directionChangeInterval.x, directionChangeInterval.y);
                circleSide = -circleSide;
            }

            // Along the circle, plus a pull towards its radius.
            var side = Vector3.Cross(TorusObject.Up, toTarget) * circleSide;
            var move = side + toTarget * ((distance - circleRadius) / Mathf.Max(0.1f, circleRadius) * radiusCorrection);
            MoveTowards(move.normalized);
        }

        private void EnterWindup()
        {
            state = State.Windup;
            stateEndTime = Time.time + windup;
            if (BodyFlash)
                BodyFlash.Telegraph(telegraphColor, windup);

            RaiseWindupStarted();
        }

        private void StartSwoop()
        {
            var offset = OffsetToTarget();
            swoopDirection = offset.sqrMagnitude > 0.0001f ? offset.normalized : TorusObject.Facing;
            swoopHit = false;
            state = State.Swoop;
            stateEndTime = Time.time + swoopDuration;
            RaiseAttackReleased();
        }

        private void UpdateSwoop()
        {
            TorusObject.MoveInDirection(swoopDirection, swoopDistance / swoopDuration * Time.deltaTime);
            if (swoopHit == false && TryHitTarget(hitRadius, damage, knockbackValue))
                swoopHit = true;

            if (Time.time >= stateEndTime)
                EndSwoop();
        }

        private void EndSwoop()
        {
            if (BodyFlash)
                BodyFlash.StopTelegraph();

            state = State.Circle;
            nextAttackTime = Time.time + Random.Range(attackInterval.x, attackInterval.y);
        }

        private void OnDisable()
        {
            if (BodyFlash)
                BodyFlash.StopTelegraph();
        }
    }
}
