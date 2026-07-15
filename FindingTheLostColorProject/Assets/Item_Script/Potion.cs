using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Consumption Settings")]
    [Tooltip("체력 혹은 물감이 가득 차있을 때도 포션을 섭취 가능한지 여부")]
    public bool consumeEvenIfFull = false;

    [Tooltip("섭취 시 포션이 재생성(리스폰)되는지 여부")]
    public bool respawnOnConsume = true;

    [Tooltip("재생성되지 않을 때, 포션 오브젝트를 씬에서 영구 삭제(Destroy)하는지 여부")]
    public bool destroyOnConsume = true;

    [Tooltip("포션 재생성 대기 시간 (초, 기본값: 5.0)")]
    public float respawnCooldown = 5f;

    [Header("Effects & Audio")]
    [Tooltip("획득 시 재생할 효과음")]
    public AudioClip collectSFX;

    [Tooltip("획득 시 생성할 파티클/이펙트 프리팹 (옵션)")]
    public GameObject collectEffectPrefab;

    [Header("Aura Effect Settings (신규)")]
    [Tooltip("물약과 함께 스폰되어 일정 주기로 깜빡일 후광/마법진 프리팹 (스프라이트 대신 게임오브젝트 이미지)")]
    public GameObject auraPrefab;
    [Tooltip("후광 깜빡임 주기/속도 (기본값: 3.0)")]
    public float auraPulseSpeed = 3f;
    [Tooltip("후광 효과의 최소 투명도 (기본값: 0.15)")]
    public float auraMinAlpha = 0.15f;
    [Tooltip("후광 효과의 최대 투명도 (기본값: 0.85)")]
    public float auraMaxAlpha = 0.85f;

    private GameObject auraInstance;              // 동적 소환된 후광 오브젝트 인스턴스

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
        SpawnAuraEffect(); // 신규 후광 효과 스폰 (spawn이펙트 프리팹 제거 후 대체)
    }

    private void Update()
    {
        if (isHoverActive)
        {
            float newY = startPos.y + Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }

        // [신규/개선] 후광 프리팹 내 자식 스프라이트들을 실시간 동적 수집하여 깜빡임 (초기화 타이밍 이슈 완전 예방)
        if (auraInstance != null && auraInstance.activeSelf)
        {
            SpriteRenderer[] renderers = auraInstance.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                float lerpValue = (Mathf.Sin(Time.time * auraPulseSpeed) + 1f) / 2f;
                float targetAlpha = Mathf.Lerp(auraMinAlpha, auraMaxAlpha, lerpValue);

                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        Color c = r.color;
                        c.a = targetAlpha; // 실시간 알파값 직접 변조 적용
                        r.color = c;
                    }
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어 판정 (PlayerHealth 컴포넌트가 있는지 확인)
        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = collision.GetComponentInChildren<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            // 섭취 자격 검증 (이미 가득 차 있는지 여부)
            if (!consumeEvenIfFull)
            {
                bool healthNeed = false;
                bool paintNeed = false;

                if (potionType == PotionType.Health || potionType == PotionType.Both)
                {
                    if (playerHealth.currentHealth < playerHealth.maxHealth)
                    {
                        healthNeed = true;
                    }
                }

                if (potionType == PotionType.Paint || potionType == PotionType.Both)
                {
                    GaugeController gauge = playerHealth.GetComponent<GaugeController>();
                    if (gauge == null) gauge = playerHealth.GetComponentInChildren<GaugeController>();
                    if (gauge == null) gauge = FindFirstObjectByType<GaugeController>();

                    if (gauge != null && gauge.currentPaint < gauge.maxPaint)
                    {
                        paintNeed = true;
                    }
                }

                // 둘 중 아무것도 부족하지 않다면 스킵
                if (!healthNeed && !paintNeed)
                {
                    return;
                }
            }

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
                GaugeController gauge = playerHealth.GetComponent<GaugeController>();
                if (gauge == null) gauge = playerHealth.GetComponentInChildren<GaugeController>();
                if (gauge == null) gauge = FindFirstObjectByType<GaugeController>();

                if (gauge != null)
                {
                    gauge.currentPaint = Mathf.Min(gauge.currentPaint + paintRestoreAmount, gauge.maxPaint);
                    consumed = true;
                }
            }

            // 포션 획득 성공 시 두둥실 상승하며 기화되는 연출 시작
            if (consumed)
            {
                StartCoroutine(CollectLingerRoutine());
            }
        }
    }

    private IEnumerator CollectLingerRoutine()
    {
        // 1. 중복 획득 차단을 위해 콜라이더들만 즉시 비활성화
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Collider2D[] childCols = GetComponentsInChildren<Collider2D>(true);
        foreach (var childCol in childCols)
        {
            if (childCol != null) childCol.enabled = false;
        }

        // 2. 호버링 둥둥 무빙 일시정지
        isHoverActive = false;

        // 3. 효과음 재생 (피치 1.2배 및 앞부분 0.15초 무음 컷 적용 오버랩 재생)
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

            float masterVol = SoundManager.Instance != null ? SoundManager.Instance.GetMasterVolume() : 0.5f;
            float sfxVol = SoundManager.Instance != null ? SoundManager.Instance.GetSFXVolume() : 0.5f;
            tempSource.volume = 0.85f * sfxVol * masterVol;

            tempSource.pitch = 1.2f;
            tempSource.time = Mathf.Clamp(0.15f, 0f, clipToPlay.length - 0.01f);
            tempSource.spatialBlend = 0f;

            tempSource.Play();
            Destroy(tempGO, ((clipToPlay.length - 0.15f) / 1.2f) + 0.1f);
        }

        // 획득 이펙트 프리팹 생성
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }

        // 4. 천천히 Y축 상승 및 투명화 (0.7초간 진행)
        float duration = 0.7f;
        float elapsed = 0f;
        Vector3 startPosForRise = transform.position;
        float riseDistance = 1.0f; // 상승할 총 높이 (1.0m)

        // 모든 SpriteRenderer 수집
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        SpriteRenderer[] childSRs = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        if (sr != null) renderers.Add(sr);
        foreach (var child in childSRs)
        {
            if (child != null) renderers.Add(child);
        }

        // 각 SpriteRenderer의 원래 색상 백업
        List<Color> startColors = new List<Color>();
        foreach (var r in renderers)
        {
            startColors.Add(r.color);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Y축 위치 보간 상승
            float newY = startPosForRise.y + Mathf.Lerp(0f, riseDistance, t);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // 알파값 페이드 아웃
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    Color c = startColors[i];
                    c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                    renderers[i].color = c;
                }
            }

            yield return null;
        }

        // 5. 상승 및 투명화 완료 후 최종 비활성화
        SetComponentsActive(false);

        // 6. 알파값 원본으로 복구 (나중에 리스폰될 때 정상적으로 보이기 위함)
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].color = startColors[i];
            }
        }

        // 7. 리스폰 설정에 따라 대기 또는 완전 파괴
        if (respawnOnConsume)
        {
            StartCoroutine(RespawnRoutine());
        }
        else if (destroyOnConsume)
        {
            Destroy(gameObject);
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

        // 재생성(리스폰)될 때 후광 효과 다시 스폰/갱신
        SpawnAuraEffect();

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

    /// <summary>
    /// [추가] 인스펙터에 지정된 auraPrefab 게임 오브젝트 이미지를 후광 인스턴스로 소환하여 Potion 뒤에 배치합니다.
    /// </summary>
    private void SpawnAuraEffect()
    {
        // 기존 후광이 잔존해있다면 파괴하여 중복 방지
        if (auraInstance != null)
        {
            Destroy(auraInstance);
            auraInstance = null;
        }

        if (auraPrefab != null)
        {
            // 후광 프리팹을 포션의 자식으로 소환하여 정렬
            auraInstance = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);
            auraInstance.transform.localPosition = Vector3.zero;

            // 소환된 후광 내의 SpriteRenderer들의 레이어 순서 바로 한 단계 뒤로 레이어 자동 조정
            SpriteRenderer mySR = GetComponent<SpriteRenderer>();
            if (mySR == null) mySR = GetComponentInChildren<SpriteRenderer>();

            SpriteRenderer[] renderers = auraInstance.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in renderers)
            {
                if (r != null && mySR != null)
                {
                    r.sortingLayerName = mySR.sortingLayerName;
                    r.sortingOrder = mySR.sortingOrder - 1; // 포션 바로 뒷배경
                }
            }
        }
    }
}
