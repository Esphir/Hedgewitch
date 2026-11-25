// Heavy melee enemy that follows the navmesh to the player and swings when close
using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Hedgewitch.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class BruiserAgent : MonoBehaviour
    {
        public enum State
        {
            Wander,
            Chase,
            WindUp,
            Strike,
            Recover
        }

        [Header("Target")]
        [Tooltip("Found by the Player tag if left empty.")]
        [SerializeField] private Transform target;

        [Tooltip("Starts chasing when the player is this close and in sight.")]
        [Min(0f)] [SerializeField] private float aggroRange = 14f;

        [Tooltip("Gives up and wanders again past this distance.")]
        [Min(0f)] [SerializeField] private float loseInterestRange = 24f;

        [SerializeField] private LayerMask sightBlockers = 1;

        [Tooltip("Seconds it keeps chasing after the player hits it, however far away or out of sight they are. Each hit restarts the timer.")]
        [Min(0f)] [SerializeField] private float provokedMemory = 8f;

        [Header("Movement")]
        [Min(0f)] [SerializeField] private float runSpeed = 5.6f;
        [Min(0f)] [SerializeField] private float wanderSpeed = 1.6f;
        [Min(0.1f)] [SerializeField] private float acceleration = 25f;
        [Min(0f)] [SerializeField] private float turnSpeed = 540f;
        [Min(0.5f)] [SerializeField] private float wanderRadius = 6f;

        [Tooltip("Seconds between path updates while chasing.")]
        [Min(0.05f)] [SerializeField] private float repathInterval = 0.2f;

        [Header("Attack")]
        [Tooltip("Starts the wind up when the player is this close.")]
        [Min(0.1f)] [SerializeField] private float attackRange = 1.8f;

        [Tooltip("Telegraph time before the swing lands.")]
        [Min(0f)] [SerializeField] private float windUpTime = 0.45f;

        [Min(0.02f)] [SerializeField] private float strikeTime = 0.18f;
        [Min(0f)] [SerializeField] private float lungeSpeed = 7f;

        [Tooltip("How far in front the hit sphere sits.")]
        [Min(0f)] [SerializeField] private float hitReach = 1.1f;

        [Min(0.1f)] [SerializeField] private float hitRadius = 1f;
        [Min(0f)] [SerializeField] private float damage = 12f;
        [Min(0f)] [SerializeField] private float knockback = 7f;
        [Min(0f)] [SerializeField] private float stagger = 0.2f;

        [Tooltip("Cooldown after a swing before chasing again.")]
        [Min(0f)] [SerializeField] private float recoverTime = 1.2f;

        [Header("Telegraph")]
        [Tooltip("Model squashed during the wind up. First child if left empty.")]
        [SerializeField] private Transform model;

        [SerializeField] private Vector3 windUpSquash = new Vector3(1.15f, 0.8f, 1.15f);

        private Rigidbody body;
        private NavMeshAgent agent;
        private Health health;
        private float provokedUntil = float.NegativeInfinity;
        private State state = State.Wander;
        private float stateTimer;
        private float repathTimer;
        private Vector3 home;
        private Vector3 wanderPoint;
        private Vector3 strikeDirection;
        private Vector3 modelRestScale = Vector3.one;
        private bool struck;
        private readonly ContactPoint[] contactBuffer = new ContactPoint[8];
        private Vector3 obstacleNormal;

        public event Action<BruiserAgent> AttackStarted;
        public event Action<BruiserAgent, IDamageable> AttackLanded;

        public State CurrentState => state;

        public Transform Target => target;

        public bool IsProvoked => Time.time < provokedUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.freezeRotation = true;

            // The agent only plans the route and physics moves the body so knockback still works
            agent = GetComponent<NavMeshAgent>();
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.avoidancePriority = Random.Range(40, 60);

            if (model == null && transform.childCount > 0)
            {
                model = transform.GetChild(0);
            }

            if (model != null)
            {
                modelRestScale = model.localScale;
            }

            home = transform.position;
            wanderPoint = home;

            // Subscribed for the whole lifetime so hits still register while a stagger has this switched off
            health = GetComponent<Health>();
            if (health != null)
            {
                health.Damaged += HandleDamaged;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }
        }

        // Getting hit by the player locks on from any range or angle
        private void HandleDamaged(DamageInfo info, DamageResult result)
        {
            Transform attacker = info.Source != null ? info.Source.transform : null;
            if (result.Defeated || attacker == null || !(attacker.CompareTag("Player") || attacker == target))
            {
                return;
            }

            target = attacker;
            provokedUntil = Time.time + provokedMemory;
            if (state == State.Wander)
            {
                Enter(State.Chase);
            }
        }

        private void OnEnable()
        {
            // Back from a stagger mid swing
            if (state == State.WindUp || state == State.Strike)
            {
                Enter(State.Recover);
            }
        }

        private void OnDisable()
        {
            ResetModel();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            stateTimer += deltaTime;
            FindTarget();
            SyncAgent();

            switch (state)
            {
                case State.Wander:
                    TickWander(deltaTime);
                    break;
                case State.Chase:
                    TickChase(deltaTime);
                    break;
                case State.WindUp:
                    TickWindUp(deltaTime);
                    break;
                case State.Strike:
                    TickStrike();
                    break;
                case State.Recover:
                    Brake();
                    if (stateTimer >= recoverTime)
                    {
                        Enter(State.Chase);
                    }

                    break;
            }

            // Contacts arrive after this step's physics so each one only steers the next step
            obstacleNormal = Vector3.zero;
        }

        // Remembers which way static walls push back so steering can slide along them
        private void OnCollisionStay(Collision collision)
        {
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic)
            {
                return;
            }

            int count = collision.GetContacts(contactBuffer);
            for (int i = 0; i < count; i++)
            {
                Vector3 normal = contactBuffer[i].normal;
                if (normal.y > 0.5f)
                {
                    continue;
                }

                normal.y = 0f;
                obstacleNormal += normal;
            }
        }

        private void TickWander(float deltaTime)
        {
            repathTimer -= deltaTime;
            if (repathTimer <= 0f || FlatDistance(transform.position, wanderPoint) < 0.8f)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);
                wanderPoint = NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : candidate;
                repathTimer = 3f;
                SetDestination(wanderPoint, wanderSpeed);
            }

            Steer(PathDirection(wanderPoint), wanderSpeed, deltaTime);

            if (CanSeeTarget(aggroRange))
            {
                Enter(State.Chase);
            }
        }

        private void TickChase(float deltaTime)
        {
            if (target == null)
            {
                Enter(State.Wander);
                return;
            }

            float distance = FlatDistance(transform.position, target.position);
            if (distance > loseInterestRange && !IsProvoked)
            {
                Enter(State.Wander);
                return;
            }

            if (distance <= attackRange && CanSeeTarget(attackRange + 1f))
            {
                Enter(State.WindUp);
                return;
            }

            repathTimer -= deltaTime;
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                SetDestination(target.position, runSpeed);
            }

            Steer(PathDirection(target.position), runSpeed, deltaTime);
        }

        private void TickWindUp(float deltaTime)
        {
            Brake();

            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 1e-4f)
                {
                    strikeDirection = toTarget.normalized;
                    Face(strikeDirection, deltaTime);
                }
            }

            if (model != null)
            {
                float t = windUpTime > 0f ? Mathf.Clamp01(stateTimer / windUpTime) : 1f;
                model.localScale = Vector3.Scale(modelRestScale, Vector3.Lerp(Vector3.one, windUpSquash, t));
            }

            if (stateTimer >= windUpTime)
            {
                Enter(State.Strike);
            }
        }

        private void TickStrike()
        {
            if (!struck)
            {
                TryHit();
            }

            if (stateTimer >= strikeTime)
            {
                Enter(State.Recover);
            }
        }

        private void Enter(State next)
        {
            state = next;
            stateTimer = 0f;
            repathTimer = 0f;

            switch (next)
            {
                case State.WindUp:
                    strikeDirection = transform.forward;
                    AttackStarted?.Invoke(this);
                    break;

                case State.Strike:
                    struck = false;
                    ResetModel();
                    Vector3 velocity = body.linearVelocity;
                    velocity.x = strikeDirection.x * lungeSpeed;
                    velocity.z = strikeDirection.z * lungeSpeed;
                    body.linearVelocity = velocity;
                    break;

                case State.Recover:
                    ResetModel();
                    break;

                case State.Wander:
                    home = transform.position;
                    break;
            }
        }

        private void TryHit()
        {
            if (target == null)
            {
                return;
            }

            Vector3 centre = transform.position + strikeDirection * hitReach;
            Collider[] hits = Physics.OverlapSphere(centre, hitRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i].transform.IsChildOf(target))
                {
                    continue;
                }

                IDamageable victim = hits[i].GetComponentInParent<IDamageable>();
                if (victim == null || !victim.IsAlive)
                {
                    continue;
                }

                victim.ApplyDamage(new DamageInfo
                {
                    Amount = damage,
                    Point = hits[i].ClosestPoint(centre),
                    Direction = strikeDirection,
                    Knockback = knockback,
                    Stagger = stagger,
                    Tags = DamageTags.Melee,
                    Source = gameObject
                });

                struck = true;
                AttackLanded?.Invoke(this, victim);
                return;
            }
        }

        private void SetDestination(Vector3 destination, float speed)
        {
            if (agent.isOnNavMesh)
            {
                agent.speed = speed;
                agent.SetDestination(destination);
            }
        }

        // Next step along the navmesh path or straight at the goal when off the mesh
        private Vector3 PathDirection(Vector3 destination)
        {
            if (agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
            {
                Vector3 desired = agent.desiredVelocity;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.01f)
                {
                    return desired;
                }

                if (agent.hasPath && agent.remainingDistance <= agent.stoppingDistance)
                {
                    return Vector3.zero;
                }
            }

            return destination - transform.position;
        }

        // Keeps the agent's idea of where it is on top of the physics body
        private void SyncAgent()
        {
            if (!agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                if (FlatDistance(agent.nextPosition, body.position) > 1.5f)
                {
                    agent.Warp(body.position);
                }
                else
                {
                    agent.nextPosition = body.position;
                }
            }
            else if (NavMesh.SamplePosition(body.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        private void Steer(Vector3 direction, float speed, float deltaTime)
        {
            direction.y = 0f;
            direction = SlideAlongObstacle(direction);
            Vector3 velocity = body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 desired = direction.sqrMagnitude > 1e-4f ? direction.normalized * speed : Vector3.zero;
            body.AddForce((desired - planar) * acceleration, ForceMode.Acceleration);

            if (desired.sqrMagnitude > 0.01f)
            {
                Face(desired, deltaTime);
            }
        }

        // Drops the part of the push that goes into a wall so the body slides round corners instead of wedging on them
        private Vector3 SlideAlongObstacle(Vector3 direction)
        {
            if (obstacleNormal.sqrMagnitude < 1e-4f || direction.sqrMagnitude < 1e-4f)
            {
                return direction;
            }

            Vector3 normal = obstacleNormal.normalized;
            float into = Vector3.Dot(direction.normalized, normal);
            if (into >= 0f)
            {
                return direction;
            }

            // Head on there is no side to slide to so keep pushing and let the path move on
            Vector3 slide = direction.normalized - normal * into;
            return slide.sqrMagnitude > 0.01f ? slide.normalized * direction.magnitude : direction;
        }

        private void Brake()
        {
            Vector3 velocity = body.linearVelocity;
            body.AddForce(new Vector3(-velocity.x, 0f, -velocity.z) * acceleration, ForceMode.Acceleration);
        }

        private void Face(Vector3 direction, float deltaTime)
        {
            Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * deltaTime);
        }

        private bool CanSeeTarget(float range)
        {
            if (target == null)
            {
                return false;
            }

            Vector3 eye = transform.position + Vector3.up * 0.8f;
            Vector3 toTarget = target.position + Vector3.up * 0.8f - eye;
            float distance = toTarget.magnitude;
            if (distance > range)
            {
                return false;
            }

            // Hitting the player's own collider still counts as seeing them
            return !Physics.Raycast(eye, toTarget / Mathf.Max(distance, 1e-4f), out RaycastHit hit, distance, sightBlockers, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(target);
        }

        private void FindTarget()
        {
            if (target != null)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void ResetModel()
        {
            if (model != null)
            {
                model.localScale = modelRestScale;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = Color.red;
            Vector3 forward = Application.isPlaying ? strikeDirection : transform.forward;
            Gizmos.DrawWireSphere(transform.position + forward * hitReach, hitRadius);
        }
    }
}
