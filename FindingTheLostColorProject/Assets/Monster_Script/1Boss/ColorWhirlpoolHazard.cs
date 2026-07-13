using UnityEngine;

public class ColorWhirlpoolHazard : MonoBehaviour
{
    // 아래 수치들은 이 컴포넌트에서 직접 조절하지 않고, 전부 BossAttack이 스폰 시점에
    // SetStats()를 통해 넘겨줌 (수치 조정 창구를 BossAttack 하나로 통일하기 위함)
    float fadeInDuration = 2f;
    [Tooltip("한 번(한 프레임) 닿을 때 주는 피해량 - 다른 보스 공격(SpikeHazard 등)과 동일한 방식")]
    float damage = 1f;
    float pullRadius = 5f;
    float pullForce = 10f;
    float minEffectiveDistance = 0.5f; // 이 거리 이내는 "중앙 구역" (더 이상 끌어당기지 않음)
    float maxPullSpeed = 8f; // AddForce가 무적/피격 상태와 무관하게 매 물리 스텝 계속 누적되어도 이 속도 이상으로는 빨라지지 않도록 제한

    [Tooltip("중앙 구역에서 플레이어 이동속도를 이 배율만큼 곱해 둔화시킴 (0.3 = 원래 속도의 30%)")]
    float centerSlowMultiplier = 0.3f;

    /// <summary>
    /// BossAttack이 소용돌이를 스폰한 직후 호출해서 모든 수치를 한 번에 전달합니다.
    /// </summary>
    public void SetStats(float fadeInDuration, float damage, float pullRadius, float pullForce, float minEffectiveDistance, float maxPullSpeed)
    {
        this.fadeInDuration = fadeInDuration;
        this.damage = damage;
        this.pullRadius = pullRadius;
        this.pullForce = pullForce;
        this.minEffectiveDistance = minEffectiveDistance;
        this.maxPullSpeed = maxPullSpeed;
    }
    private SpriteRenderer sr;
    private float fadeElapsed = 0f;
    private Color baseColor = Color.white;
    private bool colorCached = false;
    private PlayerHealth cachedPlayer; // 매 프레임 FindFirstObjectByType 호출 대신 한 번만 찾아서 재사용
    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (sr != null)
        {
            baseColor = sr.color;
            colorCached = true;
            // 처음엔 완전히 투명하게 시작해서 fadeInDuration 동안 서서히 나타남
            Color c = baseColor;
            c.a = 0f;
            sr.color = c;
        }

        cachedPlayer = FindFirstObjectByType<PlayerHealth>();
    }

    void Update()
    {
        // 1. 페이드 인 (시각적인 부분은 그대로 프레임마다 갱신)
        if (fadeElapsed < fadeInDuration)
        {
            fadeElapsed += Time.deltaTime;
            if (sr != null && colorCached)
            {
                float t = Mathf.Clamp01(fadeElapsed / fadeInDuration);
                Color c = baseColor;
                c.a = Mathf.Lerp(0f, baseColor.a, t);
                sr.color = c;
            }
        }
    }


    void FixedUpdate()
    {
        // 끌어당김/둔화 계산은 반드시 물리 스텝(FixedUpdate)에서만 처리해야 함.
        // Update()에서 처리하면 한 물리 스텝에 힘이 여러 번 중복 적용되어
        // 프레임레이트가 높을 때 플레이어가 순간적으로 튕겨나가는 문제가 있었음
        if (cachedPlayer == null) return;

        float dist = Vector2.Distance(transform.position, cachedPlayer.transform.position);
        if (dist > pullRadius) return;

        Rigidbody2D playerRb = cachedPlayer.GetComponent<Rigidbody2D>();
        if (playerRb == null) playerRb = cachedPlayer.GetComponentInParent<Rigidbody2D>();
        if (playerRb == null) return;

        // 점프(Y축)는 PlayerMove와 중력에만 맡기고, 소용돌이는 수평(X축)에만 개입해서
        // 끌려가는 도중에도 점프 궤적이 원래와 동일하게 유지되도록 함
        if (dist <= minEffectiveDistance)
        {
            // 중앙 구역: 끌어당기지 않고, 수평 속도만 매 물리 스텝마다 깎아서
            // "붙잡혀서 둔해진" 느낌을 연출 (수직 속도는 그대로 둬서 점프에 영향 없음)
            float slowedX = playerRb.linearVelocity.x * centerSlowMultiplier;
            playerRb.linearVelocity = new Vector2(slowedX, playerRb.linearVelocity.y);
        }
        else
        {
            // 중앙 구역이 아니면 중심 방향(수평)으로 힘을 가해 끌어당김.
            // 거리가 멀수록(minEffectiveDistance ~ pullRadius 구간에서) 힘이 선형으로 약해짐:
            // 중앙 구역 경계(minEffectiveDistance)에서는 100%, 바깥 경계(pullRadius)에서는 0%에 가까워짐
            float distanceRatio = Mathf.InverseLerp(pullRadius, minEffectiveDistance, dist);
            float appliedForce = pullForce * distanceRatio;

            float dx = transform.position.x - cachedPlayer.transform.position.x;
            float horizontalDir = Mathf.Abs(dx) > 0.001f ? Mathf.Sign(dx) : 0f;
            playerRb.AddForce(new Vector2(horizontalDir * appliedForce, 0f), ForceMode2D.Force);

            // 무적 시간 등으로 인해 다른 곳에서 속도가 리셋되지 않고 여러 물리 스텝 동안
            // Force가 계속 누적되더라도, 끌려가는 수평 속도가 이 이상으로는 빨라지지 않도록 제한
            // (Y축 속도는 건드리지 않아 점프 속도는 그대로 보존됨)
            float clampedX = Mathf.Clamp(playerRb.linearVelocity.x, -maxPullSpeed, maxPullSpeed);
            playerRb.linearVelocity = new Vector2(clampedX, playerRb.linearVelocity.y);
        }
    }

    // 프리팹에 붙은 실제 콜라이더 모양 그대로 피해 판정 (원형 거리 계산 대신)
    // SpikeHazard와 동일한 방식: 겹쳐 있는 동안 매 프레임 damage 값을 그대로(deltaTime 배율 없이) 적용
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
        }
    }

    // 씬 뷰에서 끌어당김 판정 반경(pullRadius)과 중앙 둔화 구역(minEffectiveDistance)을 시각적으로 표시
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.5f); // 반투명 보라색 (소용돌이 색상과 맞춤)
        Gizmos.DrawWireSphere(transform.position, pullRadius);

        Gizmos.color = new Color(1f, 1f, 1f, 0.6f); // 중앙 둔화 구역은 흰색으로 구분
        Gizmos.DrawWireSphere(transform.position, minEffectiveDistance);
    }
}