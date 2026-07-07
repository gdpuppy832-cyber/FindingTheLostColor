using UnityEngine;
// 2페이즈 암영 결계 공격(짝/홀 컬럼)의 실제 피해 판정을 담당하는 컴포넌트
public class ShadowBarrierHazard : MonoBehaviour
{
    [Tooltip("한 번 틱마다 주는 피해량")]
    public float damage = 1f;
    [Tooltip("이 영역이 유지되는 시간, 지나면 자동 파괴")]
    public float lifetime = 3f;
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
        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(damage);
            lastDamageTime = Time.time;
        }
    }
}