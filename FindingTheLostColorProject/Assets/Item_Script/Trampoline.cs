using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Trampoline : MonoBehaviour
{
    [Header("정화 설정")]
    [Tooltip("최대 체력 (가득 칠해야 트램펄린이 가동됨)")]
    public float maxHealth = 3f;

    [Tooltip("현재 체력")]
    public float currentHealth = 0f;

    [Header("트램펄린 설정")]
    [Tooltip("채색 후 플레이어가 밟았을 때 튀어오르는 Y축 속도 (플레이어 점프력 11, 중력 3 기준 2배 높이로 뛰는 기본값은 약 15.56f)")]
    public float launchVelocity = 15.56f;

    [Tooltip("정화가 완벽히 유지되는 시간 (초)")]
    public float activeDuration = 5.0f;

    [Tooltip("정화 시간이 끝난 후 다시 색을 잃어가며 돌아가는 시간 (초)")]
    public float fadeDuration = 3.0f;

    [Header("색상 변경 설정")]
    [Tooltip("채색 전 (색이 빠진 상태)의 색상")]
    public Color startColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Tooltip("채색 완료 (활성화된 상태)의 색상")]
    public Color targetColor = Color.white;

    [Header("HIT! 텍스트 폰트")]
    [Tooltip("회복할 때 팝업되는 HIT! 텍스트 폰트 (비워두면 플레이어 폰트 자동 상속)")]
    public Font hitTextFont;

    private SpriteRenderer[] allSpriteRenderers;
    private bool isPurified = false;
    private float activeTimer = 0f;
    private float fadeTimer = 0f;

    public bool IsPurified => isPurified;

    void Start()
    {
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        UpdateVisualColor();

        // 트램펄린은 시작할 때 일반적인 충돌 판정을 지니고 있어야 하므로, trigger는 꺼둡니다.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false;
        }
    }

    void Update()
    {
        // 채색이 완료된 상태에서 시간 타이머 흐름 제어
        if (isPurified)
        {
            if (activeTimer < activeDuration)
            {
                // 5초 대기 기간
                activeTimer += Time.deltaTime;
            }
            else if (fadeTimer < fadeDuration)
            {
                // 3초 점진적 색 유실 기간
                fadeTimer += Time.deltaTime;
                float fadeRatio = 1f - Mathf.Clamp01(fadeTimer / fadeDuration);
                
                // 체력을 깎아내어 시각적 및 논리적 연동
                currentHealth = maxHealth * fadeRatio;
                UpdateVisualColor();
            }
            else
            {
                // 완전히 되돌아감
                isPurified = false;
                currentHealth = 0f;
                activeTimer = 0f;
                fadeTimer = 0f;
                UpdateVisualColor();
                Debug.Log($"[Trampoline] {gameObject.name} 트램펄린 색과 능력을 잃고 초기화되었습니다.");
            }
        }
    }

    /// <summary>
    /// 물감 칠하기에 의한 회복 누적 (작동 및 3초 소등 도중 덧칠 지원)
    /// </summary>
    public void Heal(float amount)
    {
        // 대기 기간(5초) 중에는 가득 찬 상태이므로 칠할 필요 없음
        if (isPurified && activeTimer < activeDuration) return;

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

        // 다시 가득 찼다면 타이머 리셋 및 활성화 보장
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

    private void Purify()
    {
        isPurified = true;
        currentHealth = maxHealth;
        activeTimer = 0f;
        fadeTimer = 0f;

        // 트램펄린 활성화 효과음 재생 (3D 입체 음향)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyRecover, transform.position, 0.95f);
        }

        UpdateVisualColor();
        Debug.Log($"[Trampoline] {gameObject.name} 트램펄린 활성화! 이제 밟으면 점프높이의 2배로 튑니다.");
    }

    /// <summary>
    /// 충돌 접촉 시 점프 버프 강제 작동
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 정화된 상태(활성/소등 유실 도중 포함)에서만 작동
        if (!isPurified) return;

        Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            // 위에서 덤불이나 트램펄린 위로 발을 디뎠는지 노말 벡터 검증 (위에서 아래로 착지하는 판정)
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y < -0.5f) // 발로 밟음
                {
                    // 2022+버전 linearVelocity 연동 지원
                    playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, launchVelocity);

                    // 트램펄린 도약 효과음 재생
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX(SoundManager.SFXType.Jump, 1.0f);
                    }
                    
                    // 플레이어의 이중점프 카운트를 강제 리셋하여 트램펄린을 탄 후에도 다시 정상 점프를 가능하게 지원 (선택사항)
                    PlayerMove pm = collision.gameObject.GetComponent<PlayerMove>();
                    if (pm != null)
                    {
                        // 리액션 속도 갱신
                        Debug.Log("[Trampoline] 트램펄린 도약 성공!");
                    }
                    break;
                }
            }
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
