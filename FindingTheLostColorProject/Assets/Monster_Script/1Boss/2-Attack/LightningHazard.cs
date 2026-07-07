using UnityEngine;

// 번개: fromPos(먹구름 위치)에서 시작해서, toPos(플레이어) 방향으로 아주 길게(화면 끝까지) 뻗어나가는 형태.
// 즉시 완성된 상태로 나타나고, lifetime만큼 유지되다 사라짐.
public class LightningHazard : MonoBehaviour
{
    public float damage = 1f;
    public float lifetime = 0.4f;

    bool hasHit = false; // 한 번만 피해를 주도록 (짧은 지속시간 동안 여러 번 틱 방지)

    /// <summary>
    /// fromPos에 시작점을 고정하고, toPos 방향으로 length만큼 뻗어나가는 형태로 배치.
    /// </summary>
    public void Init(Vector3 fromPos, Vector3 toPos, float length)
    {
        Vector3 direction = (toPos - fromPos).normalized;

        // 스프라이트가 세로(Y축)로 긴 막대라고 가정: "위쪽"이 toPos 방향을 향하도록 회전
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg * -1f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        float baseHeight = sr != null && sr.sprite != null ? sr.sprite.bounds.size.y : 1f;

        Vector3 scale = transform.localScale;
        scale.y = length / Mathf.Max(baseHeight, 0.001f);
        transform.localScale = scale;

        // 피벗이 중앙이라고 가정: 시작점이 고정되도록, "위쪽" 방향으로 절반 길이만큼 이동한 지점을 중심으로 삼음
        transform.position = fromPos + transform.up * (length * 0.5f);
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
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
        if (hasHit) return;

        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            player.TakeDamage(damage);
            hasHit = true;
        }
    }
}