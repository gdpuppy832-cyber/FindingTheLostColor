using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[ExecuteAlways]
public class PlayerHeartUI : MonoBehaviour
{
    [Header("하트 이미지 슬롯 (왼쪽부터 순서대로 7개 연결)")]
    [Tooltip("체력을 표현할 7개의 UI Image 컴포넌트")]
    public Image[] heartImages = new Image[7];

    [Header("하트 스프라이트 에셋")]
    [Tooltip("가득 찬 하트 이미지")]
    public Sprite fullHeartSprite;

    [Tooltip("반만 차 있는 하트 이미지")]
    public Sprite halfHeartSprite;

    [Tooltip("비어 있는 하트 이미지")]
    public Sprite emptyHeartSprite;

    [Header("플레이어 체력 컴포넌트 (비워두면 씬에서 자동 검색)")]
    [Tooltip("치즈(플레이어) 오브젝트를 드래그해서 여기에 연결해 주세요")]
    public PlayerHealth playerHealth;

    [Header("피격 흔들림 설정")]
    [Tooltip("피격 시 하트가 흔들리는 시간 (기본값: 0.2초)")]
    public float shakeDuration = 0.2f;

    [Tooltip("피격 시 하트가 흔들리는 각도 크기 (기본값: 15도)")]
    public float shakeAngle = 15f;

    [Tooltip("피격 시 하트가 미세하게 수축/팽창하는 펄스 강도 (기본값: 0.25f)")]
    public float pulseScale = 0.25f;

    // 이전 프레임의 하트 상태 저장용 배열 (0: Empty, 1: Half, 2: Full)
    private int[] previousStates;
    private Coroutine[] shakeCoroutines;

    void Start()
    {
        // 씬에서 플레이어 체력 스크립트 찾기 (직접 드래그해서 연결하지 않았을 때만 검색)
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerHealth == null && Application.isPlaying)
        {
            Debug.LogError("[PlayerHeartUI] 씬에서 PlayerHealth 컴포넌트를 찾지 못했습니다! 치즈(Player) 오브젝트가 씬에 존재하고 활성화되어 있는지 확인해 주세요.");
        }

        // 동적 배열 공간 할당
        previousStates = new int[heartImages.Length];
        shakeCoroutines = new Coroutine[heartImages.Length];

        // 최대 체력에 따른 슬롯 검증
        float maxHp = (playerHealth != null) ? playerHealth.maxHealth : (float)heartImages.Length;
        int expectedHearts = Mathf.CeilToInt(maxHp);
        if (heartImages.Length != expectedHearts)
        {
            Debug.LogWarning($"[PlayerHeartUI] 연결된 하트 이미지 개수({heartImages.Length}개)가 플레이어 최대 체력({maxHp}) 기준 필요 개수({expectedHearts}개)와 다릅니다!");
        }

        // 스프라이트 에셋 누락 경고
        if ((fullHeartSprite == null || halfHeartSprite == null || emptyHeartSprite == null) && Application.isPlaying)
        {
            Debug.LogWarning("[PlayerHeartUI] 하트 스프라이트 에셋(Full/Half/Empty) 중 일부가 연결되지 않았습니다! 에셋을 인스펙터 슬롯에 연결해 주어야 흰색 네모 대신 하트 이미지가 나타납니다.");
        }

        // 초기 하트 상태 배열 세팅
        float startHp = (playerHealth != null) ? playerHealth.currentHealth : maxHp;
        for (int i = 0; i < previousStates.Length; i++)
        {
            previousStates[i] = GetHeartState(startHp, i);
        }
    }

    void Update()
    {
        // [수정] ExecuteAlways 모드 시 Start가 수행되지 않아 배열들이 null일 수 있으므로 지연 초기화(Lazy Init) 방어
        if (previousStates == null || previousStates.Length != heartImages.Length)
        {
            previousStates = new int[heartImages.Length];
        }
        if (shakeCoroutines == null || shakeCoroutines.Length != heartImages.Length)
        {
            shakeCoroutines = new Coroutine[heartImages.Length];
        }

        // 실시간으로 씬에서 플레이어 컴포넌트 검색 시도
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        // 플레이어 체력 컴포넌트가 존재하면 해당 체력을 사용하고, 없으면 기본 프리뷰 체력(최대치)으로 강제 렌더링하여 흰색 네모 방지
        float maxHp = (playerHealth != null) ? playerHealth.maxHealth : (float)heartImages.Length;
        float currentHp = (playerHealth != null) ? playerHealth.currentHealth : maxHp;

        // 7개의 하트 상태를 체력 값에 따라 개별적으로 계산하여 이미지 변경 및 흔들림 트리거
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;

            int currentState = GetHeartState(currentHp, i);
            int prevState = previousStates[i];

            // 체력이 깎이거나 회복되어서 하트 상태 수치가 변한 경우 (화면 흔들림 없음)
            if (currentState != prevState && Application.isPlaying)
            {
                // 해당 하트 흔들림 연출 개별 트리거
                if (shakeCoroutines[i] != null)
                {
                    StopCoroutine(shakeCoroutines[i]);
                }
                shakeCoroutines[i] = StartCoroutine(ShakeHeartRoutine(i));
            }

            // 상태 기록 업데이트
            previousStates[i] = currentState;

            // 스프라이트 이미지 및 활성화 적용
            if (currentState == 2)
            {
                if (fullHeartSprite != null) heartImages[i].sprite = fullHeartSprite;
            }
            else if (currentState == 1)
            {
                if (halfHeartSprite != null) heartImages[i].sprite = halfHeartSprite;
            }
            else
            {
                if (emptyHeartSprite != null) heartImages[i].sprite = emptyHeartSprite;
            }

            heartImages[i].enabled = true;
        }
    }

    /// <summary>
    /// 하트의 현재 수치적 상태를 반환합니다. (0: Empty, 1: Half, 2: Full)
    /// </summary>
    private int GetHeartState(float hp, int index)
    {
        float fullThreshold = index + 1.0f;
        float halfThreshold = index + 0.5f;

        if (hp >= fullThreshold) return 2; // Full
        if (hp >= halfThreshold) return 1; // Half
        return 0; // Empty
    }

    /// <summary>
    /// 피격당한 해당 하트를 0.2초간 부르르 흔들고 미세 펄스(확장/축소)를 주는 연출 코루틴
    /// (Layout Group 설정의 방해를 피하기 위해 회전과 스케일 값을 흔들어 구현)
    /// </summary>
    private IEnumerator ShakeHeartRoutine(int index)
    {
        Image img = heartImages[index];
        if (img == null) yield break;

        float elapsed = 0f;
        Vector3 originalScale = Vector3.one;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // 1. Z축 기준 좌우 고속 회전 흔들림 (부르르 떠는 연출)
            float angle = Random.Range(-shakeAngle, shakeAngle);
            img.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            // 2. 피격당할 때 펄스 효과 (확장 후 다시 부드럽게 복구)
            float pulse = 1.0f + Mathf.PingPong(elapsed * (5f / shakeDuration), pulseScale);
            img.transform.localScale = originalScale * pulse;

            yield return null;
        }

        // 연출 종료 시 본래 회전값과 크기로 깔끔하게 초기화 복원
        img.transform.localRotation = Quaternion.identity;
        img.transform.localScale = originalScale;
        shakeCoroutines[index] = null;
    }

    void OnDisable()
    {
        // [방탄 코드] 비활성화 시 모든 실행 중인 흔들림 강제 정지 및 안전 복구 (초기화되지 않은 상태의 NullReference 예방)
        if (shakeCoroutines != null)
        {
            for (int i = 0; i < shakeCoroutines.Length; i++)
            {
                if (shakeCoroutines[i] != null)
                {
                    StopCoroutine(shakeCoroutines[i]);
                    shakeCoroutines[i] = null;
                }
                if (heartImages != null && i < heartImages.Length && heartImages[i] != null)
                {
                    heartImages[i].transform.localRotation = Quaternion.identity;
                    heartImages[i].transform.localScale = Vector3.one;
                }
            }
        }
    }
}
