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
    public void SetStats(float fadeInDuration, float damage, float pullRadius, float pullForce, float minEffectiveDistance, float maxPullSpeed, float dashVelocityThreshold = 12f)
    {
        this.fadeInDuration = fadeInDuration;
        this.damage = damage;
        this.pullRadius = pullRadius;
        this.pullForce = pullForce;
        this.minEffectiveDistance = minEffectiveDistance;
        this.maxPullSpeed = maxPullSpeed;
        this.dashVelocityThreshold = dashVelocityThreshold;
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


    [Tooltip("플레이어의 수평 속도 절댓값이 이 값을 넘으면 대쉬 등 특수 이동 중으로 간주하고, 소용돌이가 이번 물리 스텝에 개입하지 않음 (PlayerMove를 직접 참조하지 않기 위한 간접 판별용)")]
    float dashVelocityThreshold = 12f;

    void FixedUpdate()
    {
        // 페이드 인이 끝나기 전(등장 연출 중)에는 끌어당기지 않음
        if (fadeElapsed < fadeInDuration) return;

        if (cachedPlayer == null) return;

        // 피격 무적 시간에는 소용돌이가 끌어당기지 않음
        if (cachedPlayer.IsInvincible) return;

        float dist = Vector2.Distance(transform.position, cachedPlayer.transform.position);
        if (dist > pullRadius) return;

        Rigidbody2D playerRb = cachedPlayer.GetComponent<Rigidbody2D>();
        if (playerRb == null) playerRb = cachedPlayer.GetComponentInParent<Rigidbody2D>();
        if (playerRb == null) return;


        if (Mathf.Abs(playerRb.linearVelocity.x) >= dashVelocityThreshold) return;

        if (dist <= minEffectiveDistance)
        {

            float slowedX = playerRb.linearVelocity.x * centerSlowMultiplier;
            playerRb.linearVelocity = new Vector2(slowedX, playerRb.linearVelocity.y);
        }
        else
        {

            float distanceRatio = Mathf.InverseLerp(pullRadius, minEffectiveDistance, dist);
            float appliedForce = pullForce * distanceRatio;

            float dx = transform.position.x - cachedPlayer.transform.position.x;
            float horizontalDir = Mathf.Abs(dx) > 0.001f ? Mathf.Sign(dx) : 0f;
            playerRb.AddForce(new Vector2(horizontalDir * appliedForce, 0f), ForceMode2D.Force);


            float clampedX = Mathf.Clamp(playerRb.linearVelocity.x, -maxPullSpeed, maxPullSpeed);
            playerRb.linearVelocity = new Vector2(clampedX, playerRb.linearVelocity.y);
        }
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
        // 페이드 인이 끝나기 전(등장 연출 중)에는 피해를 입히지 않음
        if (fadeElapsed < fadeInDuration) return;

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