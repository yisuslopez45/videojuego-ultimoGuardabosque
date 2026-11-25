using UnityEngine;
using UnityEngine.AI;

//Oso que deambula por su zona y huye del guardabosques cuando se acerca
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class OsoController : MonoBehaviour
{
    public ZonaOsos zona;
    public Transform guardabosque;

    public float walkSpeed = 1.6f;
    public float runSpeed = 7f;
    public float detectionRadius = 12f;
    public float safeDistance = 22f;
    public float fleeDistance = 15f;
    public float minIdleTime = 3f;
    public float maxIdleTime = 8f;
    [Range(0f, 1f)] public float sitChance = 0.3f;
    public float speedDampTime = 0.1f;

    [Header("Rugido")]
    public AudioClip rugido;
    [Tooltip("Tiempo minimo entre dos rugidos del mismo oso")]
    public float roarCooldown = 6f;
    [Tooltip("Segundos minimo y maximo entre rugidos espontaneos mientras descansa")]
    public Vector2 idleRoarInterval = new Vector2(20f, 45f);
    [Tooltip("Variacion aleatoria del tono para que no todos los osos suenen igual")]
    public float pitchVariation = 0.1f;

    enum State { Idle, Wander, Flee }

    private State state;
    private float idleTimer;
    private float fleeRepathTimer;
    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private float lastRoarTime = -Mathf.Infinity;
    private float idleRoarTimer;

    //IDs de los parametros del Animator
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int SentadoHash = Animator.StringToHash("Sentado");

    //Direcciones de escape a probar, en grados respecto a "alejarse en linea recta"
    static readonly float[] FleeAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f };

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        idleRoarTimer = Random.Range(idleRoarInterval.x, idleRoarInterval.y);

        if (zona == null)
        {
            zona = FindFirstObjectByType<ZonaOsos>();
        }
        if (guardabosque == null)
        {
            playerController player = FindFirstObjectByType<playerController>();
            if (player != null) guardabosque = player.transform;
        }

        //Cada oso empieza en un momento distinto para que no se muevan sincronizados
        EnterIdle(Random.Range(0f, maxIdleTime));
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;

        float playerDistance = guardabosque != null
            ? Vector3.Distance(Flat(transform.position), Flat(guardabosque.position))
            : Mathf.Infinity;

        if (state != State.Flee && playerDistance < detectionRadius)
        {
            EnterFlee();
        }

        switch (state)
        {
            case State.Idle:
                idleTimer -= Time.deltaTime;
                idleRoarTimer -= Time.deltaTime;
                if (idleRoarTimer <= 0f)
                {
                    idleRoarTimer = Random.Range(idleRoarInterval.x, idleRoarInterval.y);
                    Roar();
                }
                if (idleTimer <= 0f) EnterWander();
                break;

            case State.Wander:
                if (HasArrived()) EnterIdle(Random.Range(minIdleTime, maxIdleTime));
                break;

            case State.Flee:
                if (playerDistance > safeDistance)
                {
                    EnterIdle(Random.Range(minIdleTime, maxIdleTime));
                    break;
                }
                //Recalcula la ruta de escape cada medio segundo o al llegar
                fleeRepathTimer -= Time.deltaTime;
                if (fleeRepathTimer <= 0f || HasArrived()) SetFleeDestination();
                break;
        }

        //Blend Tree: 0 = reposo, 1 = caminar, 2 = correr
        float velocity = agent.velocity.magnitude;
        float animSpeed = velocity <= walkSpeed
            ? velocity / walkSpeed
            : 1f + (velocity - walkSpeed) / (runSpeed - walkSpeed);
        animator.SetFloat(SpeedHash, animSpeed, speedDampTime, Time.deltaTime);
    }

    void EnterIdle(float duration)
    {
        state = State.Idle;
        idleTimer = duration;
        agent.ResetPath();
        animator.SetBool(SentadoHash, Random.value < sitChance);
    }

    void EnterWander()
    {
        if (zona == null || !zona.RandomPoint(out Vector3 target))
        {
            EnterIdle(minIdleTime);
            return;
        }
        state = State.Wander;
        animator.SetBool(SentadoHash, false);
        agent.speed = walkSpeed;
        agent.SetDestination(target);
    }

    void EnterFlee()
    {
        state = State.Flee;
        animator.SetBool(SentadoHash, false);
        agent.speed = runSpeed;
        SetFleeDestination();
        Roar();
    }

    //Ruge si ha pasado el tiempo de espera desde el ultimo rugido
    void Roar()
    {
        if (rugido == null || Time.time - lastRoarTime < roarCooldown) return;
        lastRoarTime = Time.time;
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(rugido);
    }

    //Prueba varias direcciones alejandose del guardabosques y elige la que deja mas lejos de el,
    //sin salir de la zona (asi no se queda atascado contra el borde)
    void SetFleeDestination()
    {
        fleeRepathTimer = 0.5f;
        Vector3 away = Flat(transform.position - guardabosque.position).normalized;
        if (away == Vector3.zero) away = transform.forward;

        Vector3 bestPoint = transform.position;
        float bestDistance = -1f;
        foreach (float angle in FleeAngles)
        {
            Vector3 candidate = transform.position + Quaternion.Euler(0f, angle, 0f) * away * fleeDistance;
            if (zona != null) candidate = zona.ClampPoint(candidate);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;

            float distance = Vector3.Distance(Flat(hit.position), Flat(guardabosque.position));
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestPoint = hit.position;
            }
        }
        agent.SetDestination(bestPoint);
    }

    bool HasArrived()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, safeDistance);
    }
}
