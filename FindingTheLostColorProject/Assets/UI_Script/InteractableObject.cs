using UnityEngine;

public abstract class InteractableObject : MonoBehaviour
{
    [Header("상호작용 공통 설정")]
    [Tooltip("상호작용이 가능한 반경 (플레이어와의 최소 거리)")]
    public float interactionRadius = 2.0f;

    [Tooltip("상호작용 가능 영역에 들어왔을 때 화면에 표시할 안내 문구")]
    public string promptMessage = "W키를 눌러 상호작용";

    /// <summary>
    /// 플레이어와 이 상호작용 대상 간의 거리를 계산합니다.
    /// </summary>
    public float GetDistanceTo(Vector3 position)
    {
        return Vector2.Distance(transform.position, position);
    }

    /// <summary>
    /// 플레이어가 상호작용 가능 영역 내에 있는지 체크합니다.
    /// </summary>
    public bool IsInRange(Vector3 position)
    {
        return GetDistanceTo(position) <= interactionRadius;
    }

    /// <summary>
    /// W키를 눌러 상호작용을 실행했을 때 호출되는 추상 메서드입니다.
    /// </summary>
    public abstract void OnInteract();

    protected virtual void OnDrawGizmosSelected()
    {
        // 에디터 씬 뷰에서 상호작용 가능한 반경을 시각적으로 드로잉
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
