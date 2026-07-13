using UnityEngine;

public class Potion : MonoBehaviour
{
    public enum PotionType { Health, Paint, Both }

    [Header("Potion Settings")]
    [Tooltip("포션의 종류 (체력 회복 / 물감 회복 / 둘 다 회복)")]
    public PotionType potionType = PotionType.Health;

    [Tooltip("체력 회복량")]
    public float healthRestoreAmount = 3f;

    [Tooltip("물감(페인트) 회복량 (0.0 ~ 1.0 범위)")]
    public float paintRestoreAmount = 0.3f;

    [Header("Collection Settings")]
    [Tooltip("획득 시 포션 오브젝트를 파괴할지 여부 (재생성 사용 시 이 설정은 무시됨)")]
    public bool destroyOnConsume = true;

    [Header("Respawn Settings (재생성 설정)")]
    [Tooltip("포션 획득 후 지정된 대기 시간 뒤에 다시 재생성(리스폰)할지 여부")]
    public bool respawnOnConsume = true;

    [Tooltip("포션 재생성 대기 시간 (초, 기본값: 5.0초)")]
    public float respawnCooldown = 5f;

    [Header("Effects & Audio")]
    [Tooltip("획득 시 재생할 효과음")]
    public AudioClip collectSFX;

    [Tooltip("획득 시 생성할 파티클/이펙트 프리팹 (옵션)")]
    public GameObject collectEffectPrefab;

    [Header("Hovering Settings (Bobbing)")]
    [Tooltip("위아래 둥둥 움직임의 주파수 (주기/속도, 높을수록 빠름, 기본값: 2.0)")]
    public float hoverSpeed = 2f;
    [Tooltip("위아래 둥둥 움직임의 진폭 (높이 범위, 높을수록 더 많이 움직임, 기본값: 0.15)")]
    public float hoverAmplitude = 0.15f;

    private Vector3 startPos;
    private bool isHoverActive = true;

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        if (isHoverActive)
        {
            float newY = startPos.y + Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어 판정 (PlayerHealth 컴포넌트가 있는지 확인)
        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        
        // 만약 플레이어 본체가 아닌 자식 콜라이더나 다른 부위에 부딪혔을 수 있으므로 부모까지 검색
        if (playerHealth == null)
        {
            playerHealth = collision.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth != null && !playerHealth.IsDead)
        {
            bool consumed = false;

            // 1. 체력 회복 처리
            if (potionType == PotionType.Health || potionType == PotionType.Both)
            {
                playerHealth.Heal(healthRestoreAmount);
                consumed = true;
            }

            // 2. 물감 회복 처리
            if (potionType == PotionType.Paint || potionType == PotionType.Both)
            {
                // 플레이어 오브젝트 또는 자식에 붙은 GaugeController 검색
                GaugeController gauge = playerHealth.GetComponent<GaugeController>();
                if (gauge == null)
                {
                    gauge = playerHealth.GetComponentInChildren<GaugeController>();
                }
                // 씬 전체에서 검색 (대안)
                if (gauge == null)
                {
                    gauge = FindFirstObjectByType<GaugeController>();
                }

                if (gauge != null)
                {
                    gauge.currentPaint = Mathf.Min(gauge.currentPaint + paintRestoreAmount, gauge.maxPaint);
                    consumed = true;
                }
            }

            // 포션 획득 성공 시 연출 및 오브젝트 처리
            if (consumed)
            {
                // 효과음 재생 (피치 1.1배 및 앞부분 0.15초 무음 컷 적용 오버랩 재생)
                AudioClip clipToPlay = collectSFX;
                if (clipToPlay == null && SoundManager.Instance != null)
                {
                    clipToPlay = SoundManager.Instance.GetCachedClip(SoundManager.SFXType.PaintRecover);
                }

                if (clipToPlay != null)
                {
                    GameObject tempGO = new GameObject("TempPotionSFX");
                    tempGO.transform.position = transform.position;
                    AudioSource tempSource = tempGO.AddComponent<AudioSource>();
                    tempSource.clip = clipToPlay;

                    // SoundManager에서 볼륨 정보 가져와서 적용
                    float masterVol = SoundManager.Instance != null ? SoundManager.Instance.GetMasterVolume() : 0.5f;
                    float sfxVol = SoundManager.Instance != null ? SoundManager.Instance.GetSFXVolume() : 0.5f;
                    tempSource.volume = 0.85f * sfxVol * masterVol;

                    tempSource.pitch = 1.2f; // 1.2배속
                    tempSource.time = Mathf.Clamp(0.15f, 0f, clipToPlay.length - 0.01f); // 앞부분 0.15초 스킵 자르기
                    tempSource.spatialBlend = 0f; // 2D 음향 재생

                    tempSource.Play();
                    Destroy(tempGO, ((clipToPlay.length - 0.15f) / 1.2f) + 0.1f); // 완재생 후 파괴
                }

                // 이펙트 프리팹 생성
                if (collectEffectPrefab != null)
                {
                    Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
                }

                // 재생성 설정이 켜져있으면 리스폰 코루틴 실행
                if (respawnOnConsume)
                {
                    StartCoroutine(RespawnRoutine());
                }
                else if (destroyOnConsume)
                {
                    // 재생성 설정이 꺼져있을 때만 오브젝트 파괴
                    Destroy(gameObject);
                }
            }
        }
    }

    /// <summary>
    /// 포션을 일시적으로 숨긴 후 쿨타임 뒤에 재활성화하는 코루틴
    /// </summary>
    private System.Collections.IEnumerator RespawnRoutine()
    {
        // 1. 포션 숨기기 (렌더러 및 콜라이더 비활성화)
        SetComponentsActive(false);

        // 2. 대기 시간
        yield return new WaitForSeconds(respawnCooldown);

        // 3. 포션 다시 나타내기 (렌더러 및 콜라이더 활성화)
        SetComponentsActive(true);

        // (옵션) 재생성될 때 이펙트도 함께 생성
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    /// <summary>
    /// 포션의 스프라이트 렌더러와 콜라이더 컴포넌트들을 활성화/비활성화 처리하는 함수
    /// </summary>
    private void SetComponentsActive(bool active)
    {
        isHoverActive = active;
        if (active)
        {
            transform.position = startPos; // 원래 리스폰 정렬 위치 복원
        }
        // 본체 및 자식의 SpriteRenderer 일괄 제어
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = active;

        SpriteRenderer[] childSRs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var childSR in childSRs)
        {
            childSR.enabled = active;
        }

        // 본체 및 자식의 Collider2D 일괄 제어
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = active;

        Collider2D[] childCols = GetComponentsInChildren<Collider2D>(true);
        foreach (var childCol in childCols)
        {
            childCol.enabled = active;
        }
    }
}
