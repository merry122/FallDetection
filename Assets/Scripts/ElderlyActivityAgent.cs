using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public enum ElderlyActivity
{
    Standing,
    Walking,
    Sitting,
    StandingUp,
    Falling
}

[DefaultExecutionOrder(-1000)]
public class ElderlyActivityAgent : MonoBehaviour
{
    [Header("Scene References")]
    public Animator animator;
    public NavMeshAgent navMeshAgent;
    public Transform[] waypoints;
    public Transform chairSeat;

    [Header("Movement")]
    public float slowWalkSpeed = 0.7f;
    public float turnSpeed = 4f;
    public float waypointTolerance = 0.25f;
    public float agentRadius = 0.34f;
    public float agentHeight = 1.7f;
    public float stoppingDistance = 0.12f;
    public float navMeshSearchRadius = 2f;
    public float navMeshBindTimeout = 1f;

    [Header("Timing")]
    public float standDuration = 2f;
    public float sitDuration = 4f;
    public float standUpDuration = 1.5f;

    [Header("Animator Parameters")]
    public string speedParameter = "Speed";
    public string sittingParameter = "IsSitting";
    public string fallingParameter = "Fall";
    public string activityParameter = "Activity";

    public ElderlyActivity CurrentActivity { get; private set; } = ElderlyActivity.Standing;
    public float CurrentActivityTime => Time.time - activityStartTime;

    int waypointIndex;
    Coroutine routine;
    float activityStartTime;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = slowWalkSpeed;
            navMeshAgent.angularSpeed = 120f;
            navMeshAgent.acceleration = 2f;
            navMeshAgent.radius = agentRadius;
            navMeshAgent.height = agentHeight;
            navMeshAgent.stoppingDistance = stoppingDistance;
            navMeshAgent.autoBraking = true;
            navMeshAgent.updateRotation = false;
            navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            if (navMeshAgent.enabled && !navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.enabled = false;
            }
        }
    }

    void OnEnable()
    {
        routine = StartCoroutine(StartWhenNavigationReady());
    }

    void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }
    }

    void Update()
    {
        float speed = 0f;

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            speed = navMeshAgent.velocity.magnitude;
        }

        SetAnimatorFloat(speedParameter, speed);
        SetAnimatorBool(sittingParameter, CurrentActivity == ElderlyActivity.Sitting);
        SetAnimatorInt(activityParameter, (int)CurrentActivity);
    }

    public void TriggerFall()
    {
        StopAutomaticMovement();
        SetActivity(ElderlyActivity.Falling);

        if (HasUsableNavMeshAgent())
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }

        SetAnimatorTrigger(fallingParameter);
    }

    public void StopAutomaticMovement()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    IEnumerator ActivityLoop()
    {
        while (enabled)
        {
            yield return StandForAWhile();
            yield return WalkToNextWaypoint();
            yield return SitOnChair();
            yield return StandUpFromChair();
            yield return WalkToNextWaypoint();
        }
    }

    IEnumerator StartWhenNavigationReady()
    {
        yield return BindAgentToNavMesh();
        yield return ActivityLoop();
    }

    IEnumerator BindAgentToNavMesh()
    {
        if (navMeshAgent == null)
        {
            yield break;
        }

        EnsureRuntimeNavMeshBuilt();

        if (HasUsableNavMeshAgent())
        {
            yield break;
        }

        float endTime = Time.time + navMeshBindTimeout;
        while (Time.time <= endTime)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                navMeshAgent.enabled = true;
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(name + " could not find a baked NavMesh within " + navMeshSearchRadius + "m. Falling back to transform movement.");
    }

    void EnsureRuntimeNavMeshBuilt()
    {
        NavMeshTriangulation beforeBuild = NavMesh.CalculateTriangulation();
        if (beforeBuild.vertices != null && beforeBuild.vertices.Length > 0)
        {
            Debug.Log("NavMesh already available before " + name + " starts moving. Triangle vertex count: " + beforeBuild.vertices.Length);
            return;
        }

        NavMeshSurface surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            Debug.LogError("No NavMeshSurface found in the scene. Add one to Synthetic Elderly Home or rebuild the scene from Fall Detection > Build Elderly Home Scene.");
            return;
        }

        ConfigureRuntimeNavigation(surface);
        Debug.Log("No baked NavMesh found at runtime. Building NavMeshSurface on " + surface.name + ".");
        surface.BuildNavMesh();

        NavMeshTriangulation afterBuild = NavMesh.CalculateTriangulation();
        int vertexCount = afterBuild.vertices != null ? afterBuild.vertices.Length : 0;
        if (vertexCount > 0)
        {
            Debug.Log("Runtime NavMesh build succeeded. Triangle vertex count: " + vertexCount);
        }
        else
        {
            Debug.LogError("Runtime NavMesh build produced no triangles. Check that Room Floor has a collider and is marked walkable, and that furniture/walls are not overriding the whole floor.");
        }
    }

    void ConfigureRuntimeNavigation(NavMeshSurface surface)
    {
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.center = new Vector3(0f, 1f, 0f);
        surface.size = new Vector3(9f, 3f, 7f);

        foreach (GameObject wall in FindObjectsByNames("Back Wall", "Left Wall", "Right Wall", "Front Wall Left", "Front Wall Right", "Door Header", "Ceiling"))
        {
            AddAreaModifier(wall, 1);
        }

        GameObject furnitureRoot = GameObject.Find("Elderly Home Furniture");
        if (furnitureRoot == null)
        {
            return;
        }

        foreach (Collider collider in furnitureRoot.GetComponentsInChildren<Collider>())
        {
            string lowerName = collider.name.ToLowerInvariant();
            bool isRug = lowerName.Contains("rug");
            AddAreaModifier(collider.gameObject, isRug ? 0 : 1);

            if (!isRug)
            {
                EnsureNavMeshObstacle(collider.gameObject, collider);
            }
        }
    }

    GameObject[] FindObjectsByNames(params string[] names)
    {
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (string objectName in names)
        {
            GameObject gameObject = GameObject.Find(objectName);
            if (gameObject != null)
            {
                found.Add(gameObject);
            }
        }

        return found.ToArray();
    }

    void AddAreaModifier(GameObject gameObject, int area)
    {
        NavMeshModifier modifier = gameObject.GetComponent<NavMeshModifier>();
        if (modifier == null)
        {
            modifier = gameObject.AddComponent<NavMeshModifier>();
        }

        modifier.overrideArea = true;
        modifier.area = area;
    }

    void EnsureNavMeshObstacle(GameObject gameObject, Collider sourceCollider)
    {
        NavMeshObstacle obstacle = gameObject.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;

        if (sourceCollider is BoxCollider boxCollider)
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = boxCollider.center;
            obstacle.size = boxCollider.size;
            return;
        }

        Bounds bounds = sourceCollider.bounds;
        Vector3 lossyScale = gameObject.transform.lossyScale;
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = gameObject.transform.InverseTransformPoint(bounds.center);
        obstacle.size = new Vector3(
            SafeDivide(bounds.size.x, Mathf.Abs(lossyScale.x)),
            SafeDivide(bounds.size.y, Mathf.Abs(lossyScale.y)),
            SafeDivide(bounds.size.z, Mathf.Abs(lossyScale.z)));
    }

    float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

    IEnumerator StandForAWhile()
    {
        SetActivity(ElderlyActivity.Standing);
        yield return new WaitForSeconds(standDuration);
    }

    IEnumerator WalkToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            yield break;
        }

        Transform target = waypoints[waypointIndex % waypoints.Length];
        waypointIndex++;

        SetActivity(ElderlyActivity.Walking);

        if (HasUsableNavMeshAgent())
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.speed = slowWalkSpeed;
            SetAgentDestination(target.position);

            while (navMeshAgent.pathPending || navMeshAgent.remainingDistance > waypointTolerance)
            {
                RotateWithAgentVelocity();
                yield return null;
            }

            navMeshAgent.isStopped = true;
        }
        else
        {
            while (Vector3.Distance(transform.position, target.position) > waypointTolerance)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(direction),
                        turnSpeed * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target.position,
                    slowWalkSpeed * Time.deltaTime);

                yield return null;
            }
        }
    }

    IEnumerator SitOnChair()
    {
        if (chairSeat == null)
        {
            yield break;
        }

        SetActivity(ElderlyActivity.Walking);
        Vector3 chairDestination = new Vector3(chairSeat.position.x, transform.position.y, chairSeat.position.z);

        if (HasUsableNavMeshAgent())
        {
            navMeshAgent.isStopped = false;
            SetAgentDestination(chairDestination);

            while (navMeshAgent.pathPending || navMeshAgent.remainingDistance > waypointTolerance)
            {
                RotateWithAgentVelocity();
                yield return null;
            }

            navMeshAgent.isStopped = true;
        }
        else
        {
            while (Vector3.Distance(transform.position, chairDestination) > waypointTolerance)
            {
                Vector3 direction = chairDestination - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(direction),
                        turnSpeed * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    chairDestination,
                    slowWalkSpeed * Time.deltaTime);

                yield return null;
            }
        }

        transform.position = chairDestination;
        transform.rotation = chairSeat.rotation;
        SetActivity(ElderlyActivity.Sitting);
        yield return new WaitForSeconds(sitDuration);
    }

    IEnumerator StandUpFromChair()
    {
        SetActivity(ElderlyActivity.StandingUp);
        yield return new WaitForSeconds(standUpDuration);
        SetActivity(ElderlyActivity.Standing);
    }

    void SetActivity(ElderlyActivity activity)
    {
        if (CurrentActivity == activity)
        {
            return;
        }

        CurrentActivity = activity;
        activityStartTime = Time.time;
    }

    void SetAnimatorFloat(string parameterName, float value)
    {
        if (animator != null && HasParameter(parameterName))
        {
            animator.SetFloat(parameterName, value);
        }
    }

    void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator != null && HasParameter(parameterName))
        {
            animator.SetBool(parameterName, value);
        }
    }

    void SetAnimatorInt(string parameterName, int value)
    {
        if (animator != null && HasParameter(parameterName))
        {
            animator.SetInteger(parameterName, value);
        }
    }

    void SetAnimatorTrigger(string parameterName)
    {
        if (animator != null && HasParameter(parameterName))
        {
            animator.SetTrigger(parameterName);
        }
    }

    bool HasParameter(string parameterName)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    void SetAgentDestination(Vector3 destination)
    {
        if (!HasUsableNavMeshAgent())
        {
            return;
        }

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            navMeshAgent.SetDestination(hit.position);
            return;
        }

        Debug.LogWarning(name + " could not find a NavMesh point near destination " + destination + ".");
    }

    void RotateWithAgentVelocity()
    {
        if (!HasUsableNavMeshAgent())
        {
            return;
        }

        Vector3 direction = navMeshAgent.desiredVelocity.sqrMagnitude > 0.001f
            ? navMeshAgent.desiredVelocity
            : navMeshAgent.velocity;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            turnSpeed * Time.deltaTime);
    }

    bool HasUsableNavMeshAgent()
    {
        return navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;
    }
}
