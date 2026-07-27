using UnityEngine;

/// <summary>
/// 2페이즈 진입 시 보스 아래에 소환되는 색채 구슬.
/// NormalMonster를 상속하지 않으므로 CursorController의 붓질(OverlapCircleAll) 판정에서
/// 자동으로 제외됨 -> 붓질로는 절대 피해를 입지 않음.
/// 오직 검은 안개(추후 구현 예정)가 TakeDamage()를 직접 호출해야만 피해가 들어감.
/// </summary>
public class ColorOrb : MonoBehaviour
{
    [Tooltip("색채 구슬의 최대 체력 (기본값: 15)")]
    public float maxHealth = 15f;
    public float currentHealth;

    [Tooltip("파괴될 때 재생할 이펙트 프리팹 (선택 사항, 비워두면 이펙트 없이 그냥 사라짐)")]
    public GameObject destroyEffectPrefab;

    [Header("체력바")]
    public Transform hpBarFill;
    public float lerpSpeed = 5f;

    [Header("Floating")]
    public float floatingSpeed = 1f;
    public float floatingDistance = 0.3f;

    [Header("Glow")]
    public SpriteRenderer glowSprite;
    public float glowFadeSpeed = 2f;

    float currentFill = 1f;
    bool isDestroyed = false;

    Vector3 startPosition;

    void Awake()
    {
        currentHealth = maxHealth;

        currentFill = 1f;

        startPosition = transform.position;
    }

    /// <summary>
    /// 검은 안개 등 외부 피해 판정 로직에서 호출해야 하는 피해 처리 함수.
    /// (아직 검은 안개가 없으므로 지금은 아무도 호출하지 않음 - 나중에 검은 안개 스크립트에서
    /// GetComponent<ColorOrb>().TakeDamage(amount) 형태로 호출하면 됨)
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (isDestroyed) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);



        if (currentHealth <= 0f)
        {
            DestroyOrb();
        }
    }
    void Update()
    {
        // 체력바
        if (hpBarFill != null)
        {
            float targetFill = currentHealth / maxHealth;

            currentFill = Mathf.Lerp(
                currentFill,
                targetFill,
                Time.deltaTime * lerpSpeed
            );

            Vector3 scale = hpBarFill.localScale;
            scale.x = currentFill;
            hpBarFill.localScale = scale;
        }

        // 상하 부유
        transform.position = startPosition +
            Vector3.up * Mathf.Sin(Time.time * floatingSpeed) * floatingDistance;

        // Glow 페이드
        if (glowSprite != null)
        {
            Color color = glowSprite.color;
            color.a = (Mathf.Sin(Time.time * glowFadeSpeed) + 1f) * 0.5f;
            glowSprite.color = color;
        }
    }
    void DestroyOrb()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }


        Destroy(gameObject);
    }
}