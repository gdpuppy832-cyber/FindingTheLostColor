using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 보스가 탄환 대신 발사하는 포션.
/// Potion.cs와 획득/회복/연출 로직은 동일하지만, 위아래로 둥둥 움직이는
/// 호버링(Bobbing) 기능은 제거되었고, 탄환처럼 날아가다 일정 시간 뒤
/// 자동 소멸한다는 점이 다르다. 리스폰(respawn) 개념도 없다 - 한 번 쓰고 사라짐.
/// </summary>
public class ChasePotion : MonoBehaviour
{
    public enum PotionType { Health, Paint, Both }

    [Header("Potion Settings")]
    [Tooltip("포션의 종류 (체력 회복 / 물감 회복 / 둘 다 회복)")]
    public PotionType potionType = PotionType.Health;

    [Tooltip("체력 회복량")]
    public float healthRestoreAmount = 3f;

    [Tooltip("물감(페인트) 회복량 (0.0 ~ 1.0 범위)")]
    public float paintRestoreAmount = 0.3f;

    [Tooltip("체력 혹은 물감이 가득 차있을 때도 포션을 섭취 가능한지 여부")]
    public bool consumeEvenIfFull = false;

    [Header("Projectile Settings")]
    [Tooltip("아무에게도 먹히지 않았을 때, 생성 후 자동으로 사라지는 시간(초)")]
    public float lifetime = 6f;

    [Header("Effects & Audio")]
    [Tooltip("획득 시 재생할 효과음")]
    public AudioClip collectSFX;

    [Tooltip("획득 시 생성할 파티클/이펙트 프리팹 (옵션)")]
    public GameObject collectEffectPrefab;

    [Header("Aura Effect Settings")]
    [Tooltip("포션과 함께 스폰되어 일정 주기로 깜빡일 후광/마법진 프리팹")]
    public GameObject auraPrefab;
    [Tooltip("후광 깜빡임 주기/속도 (기본값: 3.0)")]
    public float auraPulseSpeed = 3f;
    [Tooltip("후광 효과의 최소 투명도 (기본값: 0.15)")]
    public float auraMinAlpha = 0.15f;
    [Tooltip("후광 효과의 최대 투명도 (기본값: 0.85)")]
    public float auraMaxAlpha = 0.85f;

    private GameObject auraInstance;
    private bool consumed = false;

    private void Start()
    {
        SpawnAuraEffect();

        // 아무도 먹지 않으면 lifetime 뒤에 스스로 사라짐 (탄환과 동일한 개념)
        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        // ★ 호버링(위아래 둥둥 움직임) 기능은 요청에 따라 제거됨.
        //   이동은 이 스크립트가 아니라 발사 시 부여된 Rigidbody2D velocity가 담당함.

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
                        c.a = targetAlpha;
                        r.color = c;
                    }
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (consumed) return;

        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = collision.GetComponentInChildren<PlayerHealth>();
        }

        if (playerHealth != null)
        {
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

                if (!healthNeed && !paintNeed)
                {
                    return;
                }
            }

            bool didConsume = false;

            if (potionType == PotionType.Health || potionType == PotionType.Both)
            {
                playerHealth.Heal(healthRestoreAmount);
                didConsume = true;
            }

            if (potionType == PotionType.Paint || potionType == PotionType.Both)
            {
                GaugeController gauge = playerHealth.GetComponent<GaugeController>();
                if (gauge == null) gauge = playerHealth.GetComponentInChildren<GaugeController>();
                if (gauge == null) gauge = FindFirstObjectByType<GaugeController>();

                if (gauge != null)
                {
                    gauge.currentPaint = Mathf.Min(gauge.currentPaint + paintRestoreAmount, gauge.maxPaint);
                    didConsume = true;
                }
            }

            if (didConsume)
            {
                consumed = true;
                StartCoroutine(CollectLingerRoutine());
            }
        }
    }

    private IEnumerator CollectLingerRoutine()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Collider2D[] childCols = GetComponentsInChildren<Collider2D>(true);
        foreach (var childCol in childCols)
        {
            if (childCol != null) childCol.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero; // 먹히는 순간 날아가던 것을 멈춤

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

        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }

        float duration = 0.7f;
        float elapsed = 0f;
        Vector3 startPosForRise = transform.position;
        float riseDistance = 1.0f;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        SpriteRenderer[] childSRs = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        if (sr != null) renderers.Add(sr);
        foreach (var child in childSRs)
        {
            if (child != null) renderers.Add(child);
        }

        List<Color> startColors = new List<Color>();
        foreach (var r in renderers)
        {
            startColors.Add(r.color);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float newY = startPosForRise.y + Mathf.Lerp(0f, riseDistance, t);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

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

        // 리스폰 없이 완전히 파괴 (탄환처럼 소모성)
        Destroy(gameObject);
    }

    private void SpawnAuraEffect()
    {
        if (auraInstance != null)
        {
            Destroy(auraInstance);
            auraInstance = null;
        }

        if (auraPrefab != null)
        {
            auraInstance = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);
            auraInstance.transform.localPosition = Vector3.zero;

            SpriteRenderer mySR = GetComponent<SpriteRenderer>();
            if (mySR == null) mySR = GetComponentInChildren<SpriteRenderer>();

            SpriteRenderer[] renderers = auraInstance.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in renderers)
            {
                if (r != null && mySR != null)
                {
                    r.sortingLayerName = mySR.sortingLayerName;
                    r.sortingOrder = mySR.sortingOrder - 1;
                }
            }
        }
    }
}