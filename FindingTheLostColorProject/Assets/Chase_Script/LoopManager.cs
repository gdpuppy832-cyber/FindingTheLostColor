using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 무한 루프 맵의 중심 매니저.
/// 빈 GameObject(예: "LoopManager")에 하나만 부착한다.
/// 플레이어가 도착 지점을 지나면, 등록된 모든 오브젝트를
/// (도착 지점 - 시작 지점) 거리만큼 한 프레임 안에 동일하게 왼쪽으로 되돌린다.
/// </summary>
public class LoopManager : MonoBehaviour
{
    public static LoopManager Instance { get; private set; }

    [Header("Loop Points")]
    [Tooltip("맵이 반복되는 시작 지점 (빈 GameObject 위치)")]
    public Transform startPoint;
    [Tooltip("이 지점을 플레이어가 지나면 루프가 발생 (빈 GameObject 위치)")]
    public Transform endPoint;

    [Header("Player Reference")]
    [Tooltip("자동으로 오른쪽으로 이동하는 플레이어 Transform")]
    public Transform player;

    // 매 프레임 검색 비용을 없애기 위해, 오브젝트들이 스스로 등록/해제하는 방식 사용
    private readonly List<LoopTeleportable> registered = new List<LoopTeleportable>();

    // 같은 프레임/같은 통과에 여러 번 트리거되는 것을 막기 위한 상태 플래그
    private bool hasLoopedThisPass = false;

    void Awake()
    {
        // 씬에 하나만 존재한다는 전제의 단순 싱글턴
        Instance = this;
    }

    void Update()
    {
        if (player == null || startPoint == null || endPoint == null) return;

        bool playerPastEnd = player.position.x >= endPoint.position.x;

        // 도착 지점을 "막 통과한 그 프레임"에만 1회 트리거
        if (playerPastEnd && !hasLoopedThisPass)
        {
            hasLoopedThisPass = true;
            PerformLoop();
        }
        else if (!playerPastEnd)
        {
            // 순간이동으로 플레이어가 다시 시작 지점 근처로 돌아왔으므로
            // 다음 바퀴를 위해 트리거 가능 상태로 리셋
            hasLoopedThisPass = false;
        }
    }

    /// <summary>
    /// 등록된 모든 대상을 (도착 - 시작) 거리만큼 한 프레임 안에 동일하게 되돌림.
    /// </summary>
    void PerformLoop()
    {
        Vector3 loopOffset = endPoint.position - startPoint.position;

        // 순회 중 리스트가 변경될 가능성(오브젝트가 스스로를 비활성화하는 경우 등)에 대비해
        // 스냅샷을 떠서 순회 (등록/해제 자체는 이 프레임 로직과 무관하게 안전)
        for (int i = 0; i < registered.Count; i++)
        {
            LoopTeleportable target = registered[i];
            if (target == null) continue;
            target.ApplyLoopOffset(-loopOffset);
        }
    }

    /// <summary>
    /// LoopTeleportable이 OnEnable 시점에 스스로 호출하여 등록됨.
    /// </summary>
    public void Register(LoopTeleportable target)
    {
        if (!registered.Contains(target))
            registered.Add(target);
    }

    /// <summary>
    /// LoopTeleportable이 OnDisable/파괴 시점에 스스로 호출하여 해제됨.
    /// </summary>
    public void Unregister(LoopTeleportable target)
    {
        registered.Remove(target);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (startPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(startPoint.position, 0.3f);
        }
        if (endPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(endPoint.position, 0.3f);
        }
    }
#endif
}