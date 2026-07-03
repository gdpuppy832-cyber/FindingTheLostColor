using UnityEngine;

public class PuzzlePlate : MonoBehaviour
{
    [Header("발판 설정")]
    [Tooltip("이 발판의 고유 인덱스 (빨강=0, 주황=1, 노랑=2, 초록=3, 파랑=4, 보라=5)")]
    public int plateIndex = 0;

    private bool isActivated = false;

    public bool IsActivated => isActivated;

    void Start()
    {
        // 초기 활성화 상태 초기화
        SetActivated(false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            TriggerPlate();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TriggerPlate();
        }
    }

    /// <summary>
    /// 발판을 밟았을 때 매니저에게 신호를 보냅니다.
    /// </summary>
    private void TriggerPlate()
    {
        // 이미 순서대로 밟혀서 활성화된 상태라면 중복 입력 방지
        if (isActivated) return;

        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.OnPlateStepped(this);
        }
    }

    /// <summary>
    /// 발판의 활성화 상태만 제어합니다. (스프라이트 색상 변화는 일체 관여하지 않음)
    /// </summary>
    public void SetActivated(bool active)
    {
        isActivated = active;
    }
}
