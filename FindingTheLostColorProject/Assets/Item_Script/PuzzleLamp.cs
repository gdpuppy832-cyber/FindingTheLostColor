using UnityEngine;

public class PuzzleLamp : MonoBehaviour
{
    [Header("퍼즐 설정")]
    [Tooltip("이 등불의 인덱스 (빨강=0, 주황=1, 노랑=2, 초록=3, 파랑=4, 보라=5)")]
    public int puzzleIndex = 0;

    [Tooltip("최대 체력 (가득 칠해야 등불이 일시 점등됨)")]
    public float maxHealth = 3f;

    [Tooltip("현재 체력")]
    public float currentHealth = 0f;

    [Tooltip("완전 채색 시 색상이 유지되는 시간 (초)")]
    public float keepLitDuration = 3.0f;

    [Header("색상 설정")]
    [Tooltip("평상시 (꺼져있을 때)의 어두운 색상")]
    public Color darkColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Tooltip("점등 시 빛날 목표 색상 (빨/주/노/초/파/보 중 매칭)")]
    public Color targetColor = Color.white;

    [Header("HIT! 텍스트 폰트")]
    [Tooltip("회복할 때 팝업되는 HIT! 텍스트 폰트 (비워두면 플레이어 폰트 자동 상속)")]
    public Font hitTextFont;

    private SpriteRenderer[] allSpriteRenderers;
    private GameObject lightChild;
    private bool isPurified = false;
    private bool isLockedLit = false;
    private float litTimer = 0f;

    public bool IsPurified => isPurified;
    public bool IsLockedLit => isLockedLit;

    void Start()
    {
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        
        // 자식 오브젝트 중 "Light"라는 이름이 있다면 매칭
        Transform lt = transform.Find("Light");
        if (lt != null)
        {
            lightChild = lt.gameObject;
        }

        // 초기 상태 원복
        ResetLamp();
    }

    void Update()
    {
        // 칠해져 켜진 상태이며, 퍼즐 순서 잠금이 걸리지 않은 경우에만 타이머 카운트다운 진행
        if (isPurified && !isLockedLit)
        {
            litTimer += Time.deltaTime;
            if (litTimer >= keepLitDuration)
            {
                ResetLamp();
            }
        }
    }

    /// <summary>
    /// 물감 칠하기에 의한 일시 점등 처리
    /// </summary>
    public void Heal(float amount)
    {
        // 이미 켜진 상태(임시 또는 영구 잠금)이면 더 칠하지 않음
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

        if (currentHealth >= maxHealth)
        {
            LightUp();
        }
    }

    private void UpdateVisualColor()
    {
        float ratio = currentHealth / maxHealth;
        Color currentColor = Color.Lerp(darkColor, targetColor, ratio);

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
    /// 3초 시한부 일시 점등
    /// </summary>
    private void LightUp()
    {
        isPurified = true;
        currentHealth = maxHealth;
        litTimer = 0f;

        // 등불 켜짐 효과음 재생 (3D 입체 음향)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyRecover, transform.position, 0.9f);
        }

        UpdateVisualColor();

        if (lightChild != null)
        {
            lightChild.SetActive(true);
        }

        Debug.Log($"[PuzzleLamp] {gameObject.name} 등불 일시 점등! (색상: {targetColor})");
    }

    /// <summary>
    /// 순서에 맞춰 밟았을 때 꺼지지 않도록 영구 점등 고정
    /// </summary>
    public void LockLit()
    {
        isPurified = true;
        isLockedLit = true;
        currentHealth = maxHealth;

        // 등불 고정 점등 효과음 재생 (3D 입체 음향)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyRecover, transform.position, 0.95f);
        }

        UpdateVisualColor();

        if (lightChild != null)
        {
            lightChild.SetActive(true);
        }

        Debug.Log($"[PuzzleLamp] {gameObject.name} 등불 영구 점등 잠금 완료! (순서 일치)");
    }

    /// <summary>
    /// 등불을 완전히 끄고 상태를 초기화
    /// </summary>
    public void ResetLamp()
    {
        isPurified = false;
        isLockedLit = false;
        currentHealth = 0f;
        litTimer = 0f;
        UpdateVisualColor();

        if (lightChild != null)
        {
            lightChild.SetActive(false);
        }
    }

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
