using UnityEngine;
using System.Collections;

/// <summary>
/// 소환된 후 아래 방향으로 일정한 속도로 하강/이동하며, 
/// 지정된 수명(lifeTime)이 지나면 서서히 페이드아웃 후 스스로 소멸하는 스크립트.
/// </summary>
public class TimedObject : MonoBehaviour
{
    [Header("이동 설정 (Fall / Movement Settings)")]
    [Tooltip("아래 방향으로 이동 여부")]
    public bool enableFall = true;

    [Tooltip("아래 방향으로 하강하는 속도 (초당 이동 거리)")]
    public float fallSpeed = 3.0f;

    [Header("수명 설정 (Life Time)")]
    [Tooltip("오브젝트가 유지되는 시간 (초 단위)")]
    public float lifeTime = 3.0f;

    [Header("페이드아웃 설정 (Fade Out)")]
    [Tooltip("소멸 직전 서서히 투명해지는 연출 적용 여부")]
    public bool enableFadeOut = true;

    [Tooltip("소멸 직전 페이드아웃되는 시간 (초 단위)")]
    public float fadeOutDuration = 0.5f;

    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (enableFadeOut && spriteRenderer != null)
        {
            StartCoroutine(LifeAndFadeRoutine());
        }
        else
        {
            // 간단하게 수명 후 바로 파괴
            Destroy(gameObject, lifeTime);
        }
    }

    void Update()
    {
        // 아래 방향으로 일정한 속도로 지속 하강 이동
        if (enableFall)
        {
            transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);
        }
    }

    private IEnumerator LifeAndFadeRoutine()
    {
        // 1. (수명 - 페이드아웃 시간) 동안 대기
        float holdDuration = Mathf.Max(0f, lifeTime - fadeOutDuration);
        yield return new WaitForSeconds(holdDuration);

        // 2. 남은 페이드아웃 시간 동안 서서히 투명해짐
        float elapsed = 0f;
        Color startColor = spriteRenderer.color;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = elapsed / fadeOutDuration;

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, ratio);
            spriteRenderer.color = c;

            yield return null;
        }

        // 3. 스스로 파괴
        Destroy(gameObject);
    }
}
