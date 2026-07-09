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

        Destroy(gameObject, maxLifetime);
    }

    void Update()
    {
        if (!launched) return; // 아직 궤도 회전 중이면 BossAttack이 위치를 직접 제어하므로 여기선 아무것도 안 함

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
            Destroy(gameObject); // 닿으면 구슬은 사라짐
        }
    }
}