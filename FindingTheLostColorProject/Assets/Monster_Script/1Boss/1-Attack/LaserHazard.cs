using UnityEngine;
// 레이저 공격의 실제 피해 판정을 담당하는 컴포넌트
public class LaserHazard : MonoBehaviour
{
    [Tooltip("피해 판정 간격마다 주는 피해량")]
    public float damage = 0.5f;
    [Tooltip("레이저가 유지되는 시간, 지나면 자동 파괴")]
    public float lifetime = 5f;
    [Tooltip("레이저가 생성된 후, 실제로 피격 판정이 시작되기까지의 지연 시간 (초). 레이저 자체는 즉시 보이되 이 시간 동안은 맞아도 피해가 들어가지 않음")]
    public float hitDelay = 0.3f;
    float lastDamageTime = -999f;
    float spawnTime;
    void Start()
    {
        spawnTime = Time.time; // ★ 생성 시각 기록 (hitDelay 판정 기준)
        Destroy(gameObject, lifetime);
    }
    void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }
    void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.gameObject);
    }
    void TryDamage(GameObject obj)
    {
        // ★ 생성된 지 hitDelay가 지나기 전에는 피해 판정을 아예 하지 않음
        if (Time.time - spawnTime < hitDelay) return;

        // 태그 체크 대신 PlayerHealth를 직접 탐색 (NormalMonster와 동일한 방식)
        // 플레이어의 실제 콜라이더가 자식 오브젝트에 있어서 태그가 다를 경우에도 안전하게 감지됨
        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(damage);
            lastDamageTime = Time.time;
        }
    }
}