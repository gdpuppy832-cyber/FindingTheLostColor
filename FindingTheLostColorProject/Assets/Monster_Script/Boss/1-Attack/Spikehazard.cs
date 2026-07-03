using UnityEngine;

public class SpikeHazard : MonoBehaviour
{
    [Tooltip("한 번 닿을 때 주는 피해량")]
    public float damage = 1f;

    [Tooltip("가시가 유지되는 시간, 지나면 자동 파괴")]
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
        if (!obj.CompareTag("Player")) return;


        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(damage);
            lastDamageTime = Time.time;
        }
    }
}