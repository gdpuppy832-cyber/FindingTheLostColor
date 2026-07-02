using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [Tooltip("최대 체력 (기본값: 10)")]
    public float maxHealth = 10f;
    
    [Tooltip("현재 체력")]
    public float currentHealth = 10f;

    [Header("UI Reference")]
    [Tooltip("연결할 체력바 슬라이더(Slider)")]
    public Slider hpSlider;

    [Header("UI Lerp Settings")]
    [Tooltip("체력바 게이지가 줄어드는 속도 (높을수록 빠름, 기본값: 5.0)")]
    public float hpLerpSpeed = 5f;

    [Header("Visual Effects")]
    [Tooltip("플레이어의 SpriteRenderer (비어있으면 자동으로 검색)")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("피격 시 그리기 차단 시간 (기본값: 0.01초)")]
    public float damageDrawBlockDuration = 0.01f;

    [Tooltip("피격 시 플레이어 조작 차단 시간 (기본값: 0.3초)")]
    public float damageStunDuration = 0.3f;

    [Header("Camera Shake Settings")]
    [Tooltip("피격 시 카메라 흔들림 세기 (기본값: 0.2)")]
    public float damageShakeIntensity = 0.2f;

    [Tooltip("피격 시 카메라 흔들림 시간 (기본값: 0.2초)")]
    public float damageShakeDuration = 0.2f;

    [Header("Animator Settings")]
    [Tooltip("플레이어의 Animator (비어있으면 자동으로 검색)")]
    public Animator animator;

    [Tooltip("피해 사망 시 실행할 Animator Trigger 이름")]
    public string deathTriggerName = "Die";

    [Header("Death Settings")]
    [Tooltip("사망 감지 Y축 좌표 (이 값보다 밑으로 내려가면 추락사)")]
    public float deathYThreshold = -10f;

    [Tooltip("추락 사망 시 교체할 플레이어 이미지")]
    public Sprite deathSprite;

    [Tooltip("피해 사망 시 재생할 효과음")]
    public AudioClip deathSFX;

    [Tooltip("낭떠러지 추락 사망 시 재생할 효과음")]
    public AudioClip fallSFX;

    [Tooltip("효과음 재생용 AudioSource")]
    public AudioSource sfxAudioSource;

    [Tooltip("배경 음악 정지용 AudioSource (현재 BGM이 없으므로 플레이스홀더)")]
    public AudioSource bgmAudioSource;

    [Header("Falling Bounce Settings (추락 체공 설정)")]
    [Tooltip("추락사 상승 높이 (기본값: 3.5)")]
    public float fallBounceHeight = 3.5f;

    [Tooltip("추락사 상승 시간 (기본값: 0.5초)")]
    public float fallRiseDuration = 0.5f;

    [Tooltip("추락사 정점 체공 시간 (초, 기본값: 1.0초)")]
    public float fallHoverDuration = 1.0f;

    [Tooltip("추락사 하강 중력 가속도 (절댓값이 클수록 빠르게 낙하, 기본값: -55)")]
    public float fallDownGravity = -55f;

    [Header("Debug Test (T: 데미지, H: 회복)")]
    public bool enableTestKeys = true;

    private bool isInvincible = false;
    private bool isDead = false;
    private bool isDrawBlocked = false;

    public bool IsDead => isDead;
    public bool IsDrawBlocked => isDrawBlocked;

    void Start()
    {
        // 시작 시 체력을 최대 체력으로 초기화
        currentHealth = maxHealth;

        // SpriteRenderer 자동 감지
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        // Animator 자동 감지
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        // 슬라이더 UI 초기 설정
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHealth;
            hpSlider.value = maxHealth; // 시작 시에는 즉시 가득 채움
            if (hpSlider.fillRect != null)
            {
                hpSlider.fillRect.gameObject.SetActive(true);
            }
        }
    }

    void Update()
    {
        // 낭떠러지 추락 감지 (사망하지 않았고 Y축 좌표가 기준치 이하일 때)
        // 무적 상태(isInvincible)일지라도 즉시 추락사 처리되도록 예외 없이 동작
        if (!isDead && transform.position.y < deathYThreshold)
        {
            isInvincible = false; // 무적 강제 해제
            currentHealth = 0f;

            // 낭떠러지 추락 시에도 카메라 흔들림 효과 트리거
            CameraFollow camFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (camFollow == null)
            {
                camFollow = FindFirstObjectByType<CameraFollow>();
            }
            if (camFollow != null)
            {
                camFollow.TriggerShake(damageShakeIntensity, damageShakeDuration);
            }

            Die(isFalling: true);
        }

        // 체력바 슬라이더 부드러운 감쇄 (Lerp)
        if (hpSlider != null)
        {
            if (Mathf.Abs(hpSlider.value - currentHealth) > 0.01f)
            {
                hpSlider.value = Mathf.Lerp(hpSlider.value, currentHealth, Time.deltaTime * hpLerpSpeed);
            }
            else
            {
                hpSlider.value = currentHealth;
            }

            // 체력바 필(Fill) 영역 비활성화 조건 (보간 중인 슬라이더 값이 0에 가까워지면 숨김)
            if (hpSlider.fillRect != null)
            {
                hpSlider.fillRect.gameObject.SetActive(hpSlider.value > 0.05f);
            }
        }

        // 디버그 테스트용 키 입력 처리 (사망하지 않은 상태에서만 작동)
        if (!isDead && enableTestKeys)
        {
            bool damagePressed = false;
            bool healPressed = false;

#if ENABLE_INPUT_SYSTEM
            // New Input System
            if (Keyboard.current != null)
            {
                if (Keyboard.current.tKey.wasPressedThisFrame) damagePressed = true;
                if (Keyboard.current.hKey.wasPressedThisFrame) healPressed = true;
            }
#else
            // Legacy Input Manager
            if (Input.GetKeyDown(KeyCode.T)) damagePressed = true;
            if (Input.GetKeyDown(KeyCode.H)) healPressed = true;
#endif

            if (damagePressed)
            {
                TakeDamage(3f);
            }

            if (healPressed)
            {
                Heal(1f);
            }
        }
    }

    /// <summary>
    /// 플레이어에게 데미지를 입히는 함수
    /// </summary>
    /// <param name="amount">데미지 양</param>
    public void TakeDamage(float amount)
    {
        // 이미 사망한 상태면 처리 중단
        if (isDead) return;

        // 무적 상태인 경우 데미지 무시
        if (isInvincible) return;

        // 피격 시 일정 시간 동안 그리기 차단
        StartCoroutine(BlockDrawRoutine(damageDrawBlockDuration));

        // 피격 시 일정 시간 동안 플레이어 조작 차단 (움직이지 못함)
        StartCoroutine(DamageStunRoutine(damageStunDuration));

        // 카메라 흔들림 효과 트리거
        CameraFollow camFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (camFollow == null)
        {
            camFollow = FindFirstObjectByType<CameraFollow>();
        }
        if (camFollow != null)
        {
            camFollow.TriggerShake(damageShakeIntensity, damageShakeDuration);
        }

        currentHealth -= amount;
        
        // 체력이 0 미만으로 떨어지지 않도록 Clamp 처리
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"[PlayerHealth] 데미지 {amount} 수신. 현재 체력: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die(isFalling: false);
        }
        else
        {
            // 데미지를 입고 살아있다면 0.5초간 무적 및 깜빡임 처리
            StartCoroutine(InvincibilityRoutine());
        }
    }

    /// <summary>
    /// 플레이어의 체력을 회복시키는 함수
    /// </summary>
    /// <param name="amount">회복량</param>
    public void Heal(float amount)
    {
        // 이미 사망한 상태면 처리 중단
        if (isDead) return;

        currentHealth += amount;
        
        // 체력이 최대 체력을 초과하지 않도록 Clamp 처리
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"[PlayerHealth] 체력 {amount} 회복. 현재 체력: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// 플레이어 사망 처리 함수
    /// </summary>
    /// <param name="isFalling">추락사 여부</param>
    private void Die(bool isFalling)
    {
        if (isDead) return;
        isDead = true;
        isInvincible = false;

        Debug.LogWarning(isFalling ? "[PlayerHealth] 플레이어가 낭떠러지로 추락하여 사망했습니다!" : "[PlayerHealth] 플레이어 체력이 0이 되어 사망했습니다!");

        // 1. 플레이어 조작 차단 (마우스 제외 키보드 입력 및 물리 속도 제어)
        PlayerMove playerMove = GetComponent<PlayerMove>();
        if (playerMove != null)
        {
            playerMove.SetControl(false);
        }

        // 2. 배경음 정지 및 효과음 재생
        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
        }

        AudioClip clipToPlay = isFalling ? fallSFX : deathSFX;
        if (sfxAudioSource != null && clipToPlay != null)
        {
            sfxAudioSource.PlayOneShot(clipToPlay);
        }

        if (isFalling)
        {
            // [추락사 모션] - 기존 마리오 스타일 바운스-추락 모션 실행
            // 바닥 통과 낙하 연출을 위해 Collider2D 비활성화
            Collider2D playerCollider = GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }

            // 사망 스프라이트로 이미지 변경 및 알파값 복원
            if (spriteRenderer != null && deathSprite != null)
            {
                spriteRenderer.sprite = deathSprite;
                Color tempColor = spriteRenderer.color;
                tempColor.a = 1f;
                spriteRenderer.color = tempColor;
            }

            StartCoroutine(DeathAnimationRoutine());
        }
        else
        {
            // [피해사 모션] - 지정된 애니메이션 재생
            if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
            {
                animator.SetTrigger(deathTriggerName);
            }
            else
            {
                // 애니메이터가 동작하지 않거나 비어있는 경우 사망 이미지로 백업 변경
                if (spriteRenderer != null && deathSprite != null)
                {
                    spriteRenderer.sprite = deathSprite;
                    Color tempColor = spriteRenderer.color;
                    tempColor.a = 1f;
                    spriteRenderer.color = tempColor;
                }
            }

            // 물리적 멈춤 처리
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic; // 미끄러짐 방지
            }

            // 피해 사망 연출 진행 후 재시작 대기
            StartCoroutine(DamageDeathRestartRoutine());
        }
    }

    /// <summary>
    /// 상승 -> 체공(정점에 머무름) -> 빠른 추락 순서로 진행되는 사망 연출 (추락사용)
    /// </summary>
    private System.Collections.IEnumerator DeathAnimationRoutine()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // 낭떠러지 추락 시 카메라 뷰에 맞춰 튀어오름이 보이도록 위치 보정
        Camera cam = Camera.main;
        if (cam != null)
        {
            float camY = cam.transform.position.y;
            float orthoSize = cam.orthographicSize;
            // 카메라 화면 최하단 Y 좌표에서 1유닛 위로 강제 이동하여 시작
            transform.position = new Vector3(transform.position.x, camY - orthoSize + 1.0f, transform.position.z);
        }

        // Stage 1: 상승 (Bounce Up)
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 peakPos = startPos + new Vector3(0f, fallBounceHeight, 0f);

        while (elapsed < fallRiseDuration)
        {
            float t = elapsed / fallRiseDuration;
            // Ease Out Quad 패턴 적용하여 서서히 감속하며 정점으로 상승
            t = t * (2f - t);
            transform.position = Vector3.Lerp(startPos, peakPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = peakPos;

        // Stage 2: 공중 체공 (Hover / Hang Time)
        elapsed = 0f;
        while (elapsed < fallHoverDuration)
        {
            // 미세한 부유 연출을 위한 위아래 흔들림 효과 추가
            float bobbingOffset = Mathf.Sin(elapsed * Mathf.PI * 2f) * 0.05f;
            transform.position = peakPos + new Vector3(0f, bobbingOffset, 0f);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = peakPos;

        // Stage 3: 빠른 낙하 (Fall Down Fast)
        float velocityY = 0f;
        float elapsedFall = 0f;
        float maxFallTime = 1.5f; // 화면 밖으로 충분히 빠져나갈 시간

        while (elapsedFall < maxFallTime)
        {
            velocityY += fallDownGravity * Time.deltaTime;
            transform.position += new Vector3(0f, velocityY * Time.deltaTime, 0f);
            
            elapsedFall += Time.deltaTime;
            yield return null;
        }

        // 재시작
        UnityEngine.SceneManagement.SceneManager.LoadScene("Test");
    }

    /// <summary>
    /// 피해 사망 시 재생 애니메이션 대기 및 재시작 처리 코루틴
    /// </summary>
    private System.Collections.IEnumerator DamageDeathRestartRoutine()
    {
        // 애니메이션 재생 대기 시간 (예: 2.5초)
        yield return new WaitForSeconds(2.5f);

        // 재시작
        UnityEngine.SceneManagement.SceneManager.LoadScene("Test");
    }

    /// <summary>
    /// 1.2초간 무적 및 0.3초 주기로 투명도 깜빡임(0.4 -> 0.7)을 처리하는 코루틴
    /// </summary>
    private System.Collections.IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        
        float duration = 1.2f;
        float blinkInterval = 0.3f;
        float elapsed = 0f;
        bool isAlphaLow = true; // 투명도 0.4 시작 여부

        // 기존 플레이어 컬러 백업 (알파값 복원용)
        Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        while (elapsed < duration)
        {
            if (spriteRenderer != null)
            {
                Color tempColor = spriteRenderer.color;
                tempColor.a = isAlphaLow ? 0.4f : 0.7f;
                spriteRenderer.color = tempColor;
            }

            isAlphaLow = !isAlphaLow; // 토글

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        // 색상 및 투명도 원상복구
        if (spriteRenderer != null)
        {
            Color tempColor = spriteRenderer.color;
            tempColor.a = originalColor.a;
            spriteRenderer.color = tempColor;
        }

        isInvincible = false;
    }

    /// <summary>
    /// 피격 시 일정 시간 동안 그리기(붓질)를 차단하는 코루틴
    /// </summary>
    private System.Collections.IEnumerator BlockDrawRoutine(float duration)
    {
        isDrawBlocked = true;
        yield return new WaitForSeconds(duration);
        isDrawBlocked = false;
    }

    /// <summary>
    /// 피격 시 일정 시간 동안 플레이어의 조작을 차단하는 코루틴
    /// </summary>
    private System.Collections.IEnumerator DamageStunRoutine(float duration)
    {
        PlayerMove playerMove = GetComponent<PlayerMove>();
        if (playerMove != null)
        {
            playerMove.SetControl(false);
        }

        yield return new WaitForSeconds(duration);

        // 플레이어가 살아있는 경우에만 조작권 복구
        if (!isDead && playerMove != null)
        {
            playerMove.SetControl(true);
        }
    }
}
