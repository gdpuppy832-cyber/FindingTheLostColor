using UnityEngine;
using TMPro;

[RequireComponent(typeof(Collider2D))]
public class RoseBush : MonoBehaviour
{
    [Header("정화 설정")]
    [Tooltip("최대 체력 (가득 칠해야 정화됨)")]
    public float maxHealth = 5f;

    [Tooltip("현재 체력 (0에서 시작하여 maxHealth까지 채워야 함)")]
    public float currentHealth = 0f;

    [Header("피격 설정 (플레이어)")]
    [Tooltip("플레이어가 가까이 닿았을 때(접촉 시) 입히는 피해량")]
    public float attackDamage = 0.5f;

    [Tooltip("피해를 입히는 주기/쿨타임 (초)")]
    public float attackCooldown = 1.0f;

    [Header("색상 변경 연출 설정")]
    [Tooltip("시작 시 (색이 빠진 상태)의 색상")]
    public Color startColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Tooltip("정화 완료 시 (원래 색상)의 색상")]
    public Color targetColor = Color.white;

    [Header("HIT! 텍스트 폰트 (TextMeshPro 전용)")]
    [Tooltip("회복할 때 팝업되는 HIT! 텍스트의 TMPro 폰트 에셋 (드래그 앤 드롭 가능)")]
    public TMP_FontAsset hitTextFont;

    [Tooltip("Resources 폴더 내부의 TMPro 폰트 에셋 파일명 (메타 파일 충돌 방지 백업용)")]
    public string hitTextFontResourceName = "Hakgyoansim Nadeuri TTF L SDF";

    private SpriteRenderer[] allSpriteRenderers;
    private bool isPurified = false;
    private float lastAttackTime = 0f;

    public bool IsPurified => isPurified;

    void Start()
    {
        // 폰트 에셋 슬롯이 누락(None/Missing)된 경우 Resources 폴더에서 자동으로 로드해 옵니다.
        if (hitTextFont == null && !string.IsNullOrEmpty(hitTextFontResourceName))
        {
            hitTextFont = Resources.Load<TMP_FontAsset>(hitTextFontResourceName);
            if (hitTextFont != null)
            {
                Debug.Log($"[RoseBush] Resources 폴더에서 '{hitTextFontResourceName}' 폰트 에셋을 성공적으로 자동 로드하여 복구했습니다.");
            }
            else
            {
                Debug.LogWarning($"[RoseBush] Resources 폴더 내에 '{hitTextFontResourceName}' 이름의 폰트 에셋이 보이지 않습니다. 파일명을 확인해 주세요.");
            }
        }

        // 본체 및 자식의 모든 SpriteRenderer 검색
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        // 첫 시작 시 색상 초기화
        UpdateVisualColor();

        // 덤불은 통과가 되어야 하므로, 부착된 Collider2D를 IsTrigger로 강제 설정
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 물감 공격 등으로 회복을 누적하는 함수
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

        // 현재 체력 비중에 맞춰 색상 변경
        UpdateVisualColor();

        // 정화 완료 판정
        if (currentHealth >= maxHealth)
        {
            Purify();
        }
    }

    /// <summary>
    /// 체력 비중에 맞춰 덤불의 전체 색상을 Lerp 갱신
    /// </summary>
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
    /// 정화 완료 시의 처리 (완벽한 색 회복 및 데미지 기능 영구 차단)
    /// </summary>
    private void Purify()
    {
        isPurified = true;
        currentHealth = maxHealth;

        // 정화 완료 효과음 재생 (3D 입체 음향)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyRecover, transform.position, 0.95f);
        }

        UpdateVisualColor();
        Debug.Log($"[RoseBush] {gameObject.name} 장미덤불 정화 완료! 이제 피해를 주지 않습니다.");
    }

    /// <summary>
    /// 트리거 접촉 시 플레이어에게 피해를 입히는 물리 제어
    /// </summary>
    private void OnTriggerStay2D(Collider2D collision)
    {
        // 정화되었으면 더이상 공격하지 않음
        if (isPurified) return;

        // 플레이어 태그 또는 컴포넌트 검출
        PlayerHealth player = collision.GetComponent<PlayerHealth>();
        if (player == null)
        {
            player = collision.GetComponentInParent<PlayerHealth>();
        }

        if (player != null)
        {
            // 쿨타임 주기마다 대미지 적용
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                player.TakeDamage(attackDamage);
                lastAttackTime = Time.time;
            }
        }
    }

    /// <summary>
    /// 회복 시 TextMeshPro를 이용한 HIT! 문양 팝업 생성
    /// </summary>
    private void SpawnHitText()
    {
        GameObject hitTextObj = new GameObject("HitText_Popup");
        Vector3 spawnOffset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.2f), 0f);
        hitTextObj.transform.position = transform.position + spawnOffset;

        TextMeshPro tmp = hitTextObj.AddComponent<TextMeshPro>();
        tmp.text = "HIT!";
        tmp.fontSize = 4.5f; // TextMeshPro용 폰트 크기
        tmp.color = new Color(1f, 0.7f, 0f);
        tmp.alignment = TextAlignmentOptions.Center;

        // TextMeshPro 폰트 에셋 상속/지정
        if (hitTextFont != null)
        {
            tmp.font = hitTextFont;
        }

        MeshRenderer meshRenderer = hitTextObj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = "UI";
            meshRenderer.sortingOrder = 150;
        }

        // FloatingText 컴포넌트를 달아 위로 올라가게 함
        FloatingText floatingScript = hitTextObj.AddComponent<FloatingText>();
        if (floatingScript != null)
        {
            floatingScript.Setup(new Color(1f, 0.7f, 0f), 0.8f);
        }
    }
}
