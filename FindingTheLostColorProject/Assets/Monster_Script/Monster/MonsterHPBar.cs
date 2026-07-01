using UnityEngine;
using UnityEngine.UI;

public class MonsterHPBar : MonoBehaviour
{
    [Header("몬스터 및 UI 참조")]
    [Tooltip("체력 수치를 가져올 일반 고양이 몬스터 스크립트")]
    public NormalMonster monster;

    [Tooltip("체력 게이지를 표시할 Image 컴포넌트 (Image Type이 Filled로 설정되어 있어야 함)")]
    public Image hpImage;

    [Header("게이지 연출 설정")]
    [Tooltip("게이지가 부드럽게 차오르는 보간 속도 (기본값: 5.0)")]
    public float lerpSpeed = 5f;

    [Tooltip("정화 완료(체력이 가득 참) 시 체력바 오브젝트를 자동으로 숨길지 여부")]
    public bool hideWhenPurified = true;

    [Header("체력바 단계적 업데이트 설정")]
    [Tooltip("게이지가 갱신되는 체력 단위 (n씩 찰 때만 갱신, 예: 1.0이면 1.0, 2.0 단위로 툭툭 뜀)")]
    public float healthStepUnit = 1.0f;

    [Header("뒤집힘 방지 설정")]
    [Tooltip("몬스터 캐릭터가 좌우로 뒤집혀도 머리 위 체력바는 뒤집히지 않게 고정할지 여부")]
    public bool preventFlipping = true;

    private Canvas parentCanvas;

    void Awake()
    {
        // 최상위 또는 부모에 붙어있는 Canvas 컴포넌트 탐색
        parentCanvas = GetComponent<Canvas>();
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
    }

    void Start()
    {
        // 몬스터가 설정되지 않았다면 부모 오브젝트에서 자동으로 검색
        if (monster == null)
        {
            monster = GetComponentInParent<NormalMonster>();
        }

        // 초기 게이지 수치 설정 (단계별 적용)
        if (monster != null && hpImage != null)
        {
            float steppedHealth = Mathf.Floor(monster.currentHealth / healthStepUnit) * healthStepUnit;
            if (monster.currentHealth >= monster.maxHealth) steppedHealth = monster.maxHealth;
            hpImage.fillAmount = steppedHealth / monster.maxHealth;
        }
    }

    void Update()
    {
        if (monster == null || hpImage == null) return;

        // 1. 정화 완료 시 체력바 자동 숨김 처리
        if (hideWhenPurified && monster.IsPurified)
        {
            // Canvas 컴포넌트가 있다면 Canvas 자체를 비활성화 (드로우콜 절약)
            if (parentCanvas != null && parentCanvas.enabled)
            {
                parentCanvas.enabled = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
            return;
        }

        // 2. 단계적 체력 비율 계산 및 부드러운 Lerp 적용
        float steppedHealth = Mathf.Floor(monster.currentHealth / healthStepUnit) * healthStepUnit;
        
        // 최대 체력에 도달한 경우는 무조건 100%로 보정
        if (monster.currentHealth >= monster.maxHealth)
        {
            steppedHealth = monster.maxHealth;
        }

        float targetFill = steppedHealth / monster.maxHealth;
        
        if (Mathf.Abs(hpImage.fillAmount - targetFill) > 0.002f)
        {
            hpImage.fillAmount = Mathf.Lerp(hpImage.fillAmount, targetFill, Time.deltaTime * lerpSpeed);
        }
        else
        {
            hpImage.fillAmount = targetFill;
        }

        // 3. 몬스터 좌우 반전 시 체력바 뒤집힘(거울 효과) 방지 처리
        if (preventFlipping && transform.parent != null)
        {
            Vector3 localScale = transform.localScale;
            
            // 부모의 월드 스케일(lossyScale) X값이 음수(반전됨)이면 로컬 스케일을 음수로 만들어 월드 상에서는 양수로 보이게 조정
            float parentWorldScaleX = transform.parent.lossyScale.x;
            if (parentWorldScaleX < 0f)
            {
                localScale.x = -Mathf.Abs(localScale.x);
            }
            else
            {
                localScale.x = Mathf.Abs(localScale.x);
            }
            
            transform.localScale = localScale;
        }
    }
}
