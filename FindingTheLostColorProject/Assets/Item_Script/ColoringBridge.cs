using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ColoringBridge : MonoBehaviour
{
    [Header("채색 및 정화 설정")]
    [Tooltip("최대 체력 (가득 칠해야 다리가 활성화됨)")]
    public float maxHealth = 3f;

    [Tooltip("현재 체력")]
    public float currentHealth = 0f;

    [Header("색상 변경 설정")]
    [Tooltip("채색 전 (투명하고 흐릿한 상태)의 색상")]
    public Color startColor = new Color(1f, 1f, 1f, 0.2f);

    [Tooltip("채색 완료 (선명하고 단단한 상태)의 색상")]
    public Color targetColor = Color.white;

    [Header("HIT! 텍스트 폰트 (TextMeshPro 전용)")]
    [Tooltip("회복할 때 팝업되는 HIT! 텍스트 폰트 (비워두면 플레이어 폰트 자동 상속)")]
    public TMP_FontAsset hitTextFont;

    [Tooltip("Resources 폴더 내부의 TMPro 폰트 에셋 파일명")]
    public string hitTextFontResourceName = "Hakgyoansim Nadeuri TTF L SDF";

    private SpriteRenderer[] allSpriteRenderers;
    private Collider2D bridgeCollider;
    private bool isPurified = false;

    public bool IsPurified => isPurified;

    void Start()
    {
        // 폰트 에셋 슬롯이 누락(None/Missing)된 경우 Resources 폴더에서 자동으로 로드해 옵니다.
        if (hitTextFont == null && !string.IsNullOrEmpty(hitTextFontResourceName))
        {
            hitTextFont = Resources.Load<TMP_FontAsset>(hitTextFontResourceName);
        }

        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bridgeCollider = GetComponent<Collider2D>();

        // 시작 시 색상 초기화 및 충돌 판정 비활성화 (IsTrigger = true 상태로 통과하게 함)
        UpdateVisualColor();
        SetSolidState(false);
    }

    /// <summary>
    /// 물감 칠하기에 의한 회복 누적
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

        // 100% 채색 시 정화 완료 처리
        if (currentHealth >= maxHealth)
        {
            Purify();
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

    /// <summary>
    /// 물리적 단단함 제어 (정화 전에는 IsTrigger=true로 뚫리게 하고, 정화 후에는 IsTrigger=false로 딛고 설 수 있게 함)
    /// </summary>
    private void SetSolidState(bool isSolid)
    {
        if (bridgeCollider != null)
        {
            bridgeCollider.isTrigger = !isSolid;
        }
    }

    private void Purify()
    {
        isPurified = true;
        currentHealth = maxHealth;
        UpdateVisualColor();
        SetSolidState(true); // 이제 단단해져서 건널 수 있는 다리가 됨
        Debug.Log($"[ColoringBridge] {gameObject.name} 채색다리 활성화! 이제 건널 수 있습니다.");
    }

    private void SpawnHitText()
    {
        GameObject hitTextObj = new GameObject("HitText_Popup");
        Vector3 spawnOffset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.2f), 0f);
        hitTextObj.transform.position = transform.position + spawnOffset;

        // TextMeshPro 추가 및 세팅
        TMPro.TextMeshPro tmp = hitTextObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = "HIT!";
        tmp.fontSize = 4.5f; // TextMeshPro용 폰트 크기
        tmp.color = new Color(1f, 0.7f, 0f);
        tmp.alignment = TMPro.TextAlignmentOptions.Center;

        TMP_FontAsset appliedFont = hitTextFont;
        if (appliedFont == null)
        {
            PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();
            if (playerInt != null && playerInt.customTMPFont != null)
            {
                appliedFont = playerInt.customTMPFont;
            }
        }

        if (appliedFont != null)
        {
            tmp.font = appliedFont;
        }

        MeshRenderer meshRenderer = hitTextObj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = "UI";
            meshRenderer.sortingOrder = 150;
        }

        FloatingText floatingScript = hitTextObj.AddComponent<FloatingText>();
        if (floatingScript != null)
        {
            floatingScript.Setup(new Color(1f, 0.7f, 0f), 0.8f);
        }
    }
}
