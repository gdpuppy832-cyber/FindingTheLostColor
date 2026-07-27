using UnityEngine;


public class DarkOrbHazard : MonoBehaviour
{
    public float damage = 1f;
    public float maxLifetime = 8f; // 혹시 아무것도 못 맞추고 화면 밖으로 날아갈 경우를 대비한 안전장치

    float trackDuration;
    float speed;
    Transform trackTarget;

    bool launched = false;
    bool tracking = false;
    float trackTimer = 0f;
    Vector2 currentDirection;

    Animator animator;
    Collider2D[] colliders;
    Rigidbody2D rb;

    bool isDestroying = false;
    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        colliders = GetComponentsInChildren<Collider2D>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = GetComponentInChildren<Rigidbody2D>();
    }

    public void Launch(Transform target, float trackDuration, float speed)
    {
        this.trackTarget = target;
        this.trackDuration = trackDuration;
        this.speed = speed;

        // 발사 시점의 목표 방향으로 초기 방향을 설정 (Update 첫 프레임부터 바로 이동하도록)
        if (trackTarget != null)
        {
            currentDirection = ((Vector2)trackTarget.position - (Vector2)transform.position).normalized;
        }

        tracking = true;
        launched = true;
        trackTimer = 0f;

        StartCoroutine(DestroyAfterLifetime());
    }

    void Update()
    {
        if (!launched || isDestroying) return;

        if (tracking)
        {
            trackTimer += Time.deltaTime;

            if (trackTarget != null)
            {
                currentDirection = ((Vector2)trackTarget.position - (Vector2)transform.position).normalized;
            }

            if (trackTimer >= trackDuration)
            {
                tracking = false; // 추적 종료 -> 이 순간의 currentDirection으로 이후 계속 직진
            }
        }

        transform.position += (Vector3)(currentDirection * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamage(collision.gameObject);
    }

    void TryDamage(GameObject obj)
    {
        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            player.TakeDamage(damage);
            BeginDestroy();
        }
    }
    System.Collections.IEnumerator DestroyAfterLifetime()
    {
        yield return new WaitForSeconds(maxLifetime);

        BeginDestroy();
    }

    void BeginDestroy()
    {
        if (isDestroying)
            return;

        isDestroying = true;

        launched = false;
        tracking = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        if (colliders != null)
        {
            foreach (Collider2D c in colliders)
            {
                if (c != null)
                    c.enabled = false;
            }
        }

        StartCoroutine(DestroyRoutine());
    }

    System.Collections.IEnumerator DestroyRoutine()
    {
        float animLength = 0f;

        if (animator != null)
        {
            animator.SetTrigger("IsDestroying");

            yield return null;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            animLength = state.length;
        }

        if (animLength > 0f)
            yield return new WaitForSeconds(animLength);

        Destroy(gameObject);
    }
}