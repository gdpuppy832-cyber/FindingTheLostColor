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
        // ★ 크기는 더 이상 계산하지 않음 - 프리팹에 설정된 localScale을 그대로 사용함
        // 회전은 항상 수직(위→아래)으로 고정
        // toPos: 플레이어 x좌표 기준으로 땅에 닿는 지점 (BossAttack에서 미리 계산해서 넘겨줌)
        transform.rotation = Quaternion.identity;
        transform.position = toPos;
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