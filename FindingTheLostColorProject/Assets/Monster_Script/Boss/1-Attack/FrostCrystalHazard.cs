using UnityEngine;

// 서리 수정 하나의 낙하 및 피해/파괴 로직을 담당
// - 낙하: 자체적으로 가속하며 아래로 이동
// - 파괴: 이 오브젝트 자신의 콜라이더가 groundLayer에 닿으면 부서짐(파괴)
// - 피해 판정: 별도의 히트박스 오브젝트(ContactRelay)가 연결되어 있으면 그걸로 판정,
//   없으면 자기 콜라이더로 직접 판정 (프리팹 준비 전 임시 테스트용)
public class FrostCrystalHazard : MonoBehaviour
{
    [Tooltip("낙하 시작 속도")]
    public float initialSpeed = 0f;

    [Tooltip("낙하 가속도 (클수록 점점 빨라짐)")]
    public float acceleration = 15f;

    [Tooltip("플레이어에게 주는 피해량")]
    public float damage = 1f;

    [Tooltip("바닥에 닿지 못했을 때를 대비한 안전 장치용 최대 생존 시간")]
    public float maxLifetime = 6f;

    [Tooltip("이 레이어에 닿으면 바닥으로 판정해 부서짐(파괴)")]
    public LayerMask groundLayer;

    float currentSpeed;
    bool hasDealtDamage = false; // 한 번만 피해를 주기 위한 플래그
    ContactRelay hitboxRelay;    // 별도 히트박스 오브젝트 (BossAttack이 생성 직후 연결해줌)
    Quaternion fixedRotation;    // 스폰 시점의 회전값 (디자인된 기울기) - 이후 계속 이 값으로 고정
    void Start()
    {
        currentSpeed = initialSpeed;
        fixedRotation = transform.rotation; // 스폰 시점(프리팹이 원래 갖고 있던) 회전을 기억해둠
        Destroy(gameObject, maxLifetime);
    }

    // BossAttack이 생성 직후 호출해서 별도 히트박스를 연결함 (없으면 호출 안 해도 됨)
    public void SetHitboxRelay(ContactRelay relay)
    {
        hitboxRelay = relay;
        if (hitboxRelay != null)
        {
            hitboxRelay.onTriggerEnter += TryDamagePlayer;
        }
    }

    void Update()
    {
        currentSpeed += acceleration * Time.deltaTime;
        transform.position += Vector3.down * currentSpeed * Time.deltaTime;
        transform.rotation = fixedRotation; // 스폰 시점 회전(디자인된 기울기)으로 매 프레임 고정 - 낙하 중 도는 것만 방지
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 바닥/발판에 닿으면 부서짐
        if (((1 << other.gameObject.layer) & groundLayer) != 0)
        {
            Destroy(gameObject);
            return;
        }

        // 별도 히트박스가 연결되어 있지 않을 때만 자기 콜라이더로 직접 피격 판정
        if (hitboxRelay == null)
        {
            TryDamagePlayer(other);
        }
    }

    void TryDamagePlayer(Collider2D other)
    {
        if (hasDealtDamage) return;

        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null) player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(damage);
            hasDealtDamage = true;
        }
    }
}