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
    float spawnTime;

    void Start()
    {
        spawnTime = Time.time;
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
        // 생성 직후에는 피해 없음
        if (Time.time - spawnTime < hitDelay)
            return;

        // PlayerHealth를 직접 탐색
        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null)
            player = obj.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            player.TakeDamage(damage);
        }
    }
}