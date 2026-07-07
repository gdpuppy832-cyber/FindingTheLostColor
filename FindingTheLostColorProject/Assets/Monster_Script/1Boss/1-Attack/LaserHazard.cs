using UnityEngine;
// 레이저 공격의 실제 피해 판정을 담당하는 컴포넌트
public class LaserHazard : MonoBehaviour
{
    [Tooltip("피해 판정 간격마다 주는 피해량")]
    public float damage = 0.5f;
    [Tooltip("레이저가 유지되는 시간, 지나면 자동 파괴")]
    public float lifetime = 5f;
    float lastDamageTime = -999f;
    void Start()
    {
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