using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아래로 하강(Fall)하는 채색 발판 블록 스크립트.
/// TimedObject처럼 계속 아래로 이동하면서 수명 관리를 받으며,
/// ColoringBridge처럼 지정된 체력(기본 5)만큼 붓질/정화를 완료해야 
/// 콜라이더가 실체화(Solid)되어 플레이어가 딛고 설 수 있는 발판이 됩니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ColoringFallingBlock : MonoBehaviour
{
    [Header("1. 이동 설정 (Fall Movement)")]
    [Tooltip("아래 방향으로 지속적으로 이동할지 여부")]
    public bool enableFall = true;

    [Tooltip("아래 방향 하강 속도 (초당 이동 거리)")]
    public float fallSpeed = 3.0f;

    [Header("2. 수명 및 풀링 설정 (Life & Pooling)")]
    [Tooltip("오브젝트 수명 시간 (초 단위)")]
    public float lifeTime = 4.0f;

    [Tooltip("수명 만료 직전 페이드아웃 적용 여부")]
    public bool enableFadeOut = true;

    [Tooltip("페이드아웃 되는 시간 (초 단위)")]
    public float fadeOutDuration = 0.5f;

    [Tooltip("수명 만료 시 Destroy 대신 SetActive(false)로 풀 반납 (렉 방지)")]
    public bool useObjectPooling = true;

    [Header("3. 채색 및 정화 설정 (Purification)")]
    [Tooltip("정화 완료에 필요한 최대 체력 (기본값: 5.0)")]
    public float maxHealth = 5.0f;

    [Tooltip("현재 체력")]
    public float currentHealth = 0.0f;

    [Header("4. 색상 및 스프라이트 설정")]
    [Tooltip("채색 전 (색이 빠지고 투명한 상태)의 색상")]
    public Color startColor = new Color(1f, 1f, 1f, 0.25f);

    [Tooltip("채색 완료 (선명하고 단단한 상태)의 색상")]
    public Color targetColor = Color.white;

    [Tooltip("기본 미완성 상태의 스프라이트 (선택)")]
    public Sprite defaultSprite;

    [Tooltip("정화 완료(완성) 상태의 스프라이트 (선택)")]
    public Sprite purifiedSprite;

    [Tooltip("스프라이트 변경 대상 SpriteRenderer (비워두면 자동 탐색)")]
    public SpriteRenderer targetSpriteRenderer;

    [Header("5. HIT! 텍스트 폰트")]
    [Tooltip("회복 시 팝업되는 HIT! 텍스트 폰트")]
    public Font hitTextFont;

    private SpriteRenderer[] allSpriteRenderers;
    private Collider2D blockCollider;
    private bool isPurified = false;
    private Coroutine activeLifeRoutine;
    private int originalLayer;

    public bool IsPurified => isPurified;

    void Awake()
    {
        blockCollider = GetComponent<Collider2D>();
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalLayer = gameObject.layer;

        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
            if (targetSpriteRenderer == null)
            {
                targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }
    }

    void OnEnable()
    {
        // 1. 상태 및 체력 리셋
        isPurified = false;
        currentHealth = 0f;
        gameObject.layer = originalLayer;

        UpdateVisualColor();
        UpdateSprite();
        SetSolidState(false); // 초기에는 콜라이더 isTrigger = true (못 밟음)

        // 2. 수명 및 페이드아웃 루틴 구동
        if (activeLifeRoutine != null)
        {
            StopCoroutine(activeLifeRoutine);
        }

        if (enableFadeOut)
        {
            activeLifeRoutine = StartCoroutine(LifeAndFadeRoutine());
        }
        else
        {
            activeLifeRoutine = StartCoroutine(LifeOnlyRoutine());
        }
    }

    void OnDisable()
    {
        if (activeLifeRoutine != null)
        {
            StopCoroutine(activeLifeRoutine);
            activeLifeRoutine = null;
        }
    }

    void Update()
    {
        // 아래 방향으로 일정한 속도로 하강 이동
        if (enableFall)
        {
            transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// 플레이어 붓질/탄환/궁극기에 의한 정화 힐 수신 (5만큼 채워야 정화 완료)
    /// </summary>
    public void Heal(float amount)
    {
        if (isPurified) return;

        int oldIntHealth = Mathf.FloorToInt(currentHealth);
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        int newIntHealth = Mathf.FloorToInt(currentHealth);

        // 1 회복할 때마다 HIT! 텍스트 생성
        if (newIntHealth > oldIntHealth)
        {
            for (int i = oldIntHealth + 1; i <= newIntHealth; i++)
            {
                SpawnHitText();
            }
        }

        UpdateVisualColor();

        // 5 (maxHealth) 이상 채워지면 정화 완료 처리
        if (currentHealth >= maxHealth)
        {
            Purify();
        }
    }

    /// <summary>
    /// 정화 완료 처리 (색상 복구 + 스프라이트 교체 + 콜라이더 실체화 ➔ 밟기 가능)
    /// </summary>
    public void Purify()
    {
        if (isPurified) return;

        isPurified = true;
        currentHealth = maxHealth;

        // 정화 효과음 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyRecover, transform.position, 0.9f);
        }

        UpdateVisualColor();
        UpdateSprite();
        SetSolidState(true); // IsTrigger = false 로 변경되어 발판으로 단단해짐!

        // 레이어를 "Platform"으로 승격시켜 발밑 박스캐스트/지형 판정에 걸리도록 함
        int platformLayer = LayerMask.NameToLayer("Platform");
        if (platformLayer != -1)
        {
            SetLayerRecursively(gameObject, platformLayer);
        }
    }

    /// <summary>
    /// 물리적 단단함 제어 (정화 전: IsTrigger=true로 뚫림, 정화 후: IsTrigger=false로 딛고 덤)
    /// </summary>
    private void SetSolidState(bool isSolid)
    {
        if (blockCollider != null)
        {
            blockCollider.isTrigger = !isSolid;
        }
    }

    private void UpdateVisualColor()
    {
        float ratio = currentHealth / maxHealth;
        Color currentColor = Color.Lerp(startColor, targetColor, ratio);

        if (allSpriteRenderers != null)
        {
            foreach (var sr in allSpriteRenderers)
            {
                if (sr != null)
                {
                    sr.color = currentColor;
                }
            }
        }
    }

    private void UpdateSprite()
    {
        if (targetSpriteRenderer == null) return;

        if (isPurified && purifiedSprite != null)
        {
            targetSpriteRenderer.sprite = purifiedSprite;
        }
        else if (!isPurified && defaultSprite != null)
        {
            targetSpriteRenderer.sprite = defaultSprite;
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    #region Life & Fade Coroutines (TimedObject 라이프사이클)

    private IEnumerator LifeOnlyRoutine()
    {
        yield return new WaitForSeconds(lifeTime);
        DeactivateOrDestroy();
    }

    private IEnumerator LifeAndFadeRoutine()
    {
        float holdDuration = Mathf.Max(0f, lifeTime - fadeOutDuration);
        yield return new WaitForSeconds(holdDuration);

        float elapsed = 0f;
        Color baseStartColor = allSpriteRenderers != null && allSpriteRenderers.Length > 0 && allSpriteRenderers[0] != null
            ? allSpriteRenderers[0].color
            : targetColor;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = elapsed / fadeOutDuration;

            if (allSpriteRenderers != null)
            {
                foreach (var sr in allSpriteRenderers)
                {
                    if (sr != null)
                    {
                        Color c = sr.color;
                        c.a = Mathf.Lerp(baseStartColor.a, 0f, ratio);
                        sr.color = c;
                    }
                }
            }

            yield return null;
        }

        DeactivateOrDestroy();
    }

    private void DeactivateOrDestroy()
    {
        if (useObjectPooling)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    private void SpawnHitText()
    {
        GameObject hitTextObj = new GameObject("HitText_Popup");
        Vector3 spawnOffset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.2f), 0f);
        hitTextObj.transform.position = transform.position + spawnOffset;

        TextMesh textMesh = hitTextObj.AddComponent<TextMesh>();
        textMesh.text = "HIT!";
        textMesh.fontSize = 36;
        textMesh.characterSize = 0.16f;
        textMesh.color = new Color(1f, 0.7f, 0f);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;

        Font appliedFont = hitTextFont;
        if (appliedFont == null)
        {
            PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();
            if (playerInt != null && playerInt.customFont != null)
            {
                appliedFont = playerInt.customFont;
            }
        }

        if (appliedFont != null)
        {
            textMesh.font = appliedFont;
        }

        MeshRenderer meshRenderer = hitTextObj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = "UI";
            meshRenderer.sortingOrder = 150;
            if (appliedFont != null)
            {
                meshRenderer.material = appliedFont.material;
            }
        }

        FloatingText floatingScript = hitTextObj.AddComponent<FloatingText>();
        if (floatingScript != null)
        {
            floatingScript.Setup(new Color(1f, 0.7f, 0f), 0.8f);
        }
    }
}
