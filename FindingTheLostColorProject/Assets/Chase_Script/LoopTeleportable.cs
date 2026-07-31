using UnityEngine;

/// <summary>
/// 무한 루프 시 함께 순간이동해야 하는 모든 오브젝트에 부착.
/// 대상: 플레이어, 메인 카메라, 적, 발사체, 이펙트 등.
/// 프리팹에 미리 붙여두면, 런타임에 Instantiate되는 총알/적도
/// OnEnable 시점에 자동으로 LoopManager에 등록됨.
/// </summary>
[DisallowMultipleComponent]
public class LoopTeleportable : MonoBehaviour
{
    [Header("Optional Components")]
    [Tooltip("순간이동 직후 이전 위치와의 잔상(streak)을 지우기 위해 TrailRenderer를 자동으로 찾아 처리할지 여부")]
    public bool autoClearTrail = true;
    [Tooltip("순간이동 직후 파티클 잔상을 정리할지 여부 (World 시뮬레이션 스페이스인 경우 권장)")]
    public bool autoClearParticles = false;

    private Rigidbody2D rb2D;
    private TrailRenderer trail;
    private ParticleSystem[] particleSystems;

    void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = GetComponentInChildren<TrailRenderer>();

        if (autoClearParticles)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    void OnEnable()
    {
        // LoopManager가 이 오브젝트보다 먼저 초기화되어 있어야 하므로,
        // Script Execution Order에서 LoopManager를 더 이르게 두는 것을 권장
        if (LoopManager.Instance != null)
        {
            LoopManager.Instance.Register(this);
        }
    }

    void OnDisable()
    {
        if (LoopManager.Instance != null)
        {
            LoopManager.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// LoopManager가 루프 발생 시 호출. offset만큼 위치를 즉시(한 프레임 내) 이동시킴.
    /// </summary>
    public void ApplyLoopOffset(Vector3 offset)
    {
        if (rb2D != null)
        {
            // Rigidbody2D가 있는 경우 rb.position을 직접 갱신해야
            // 물리 시뮬레이션과 Transform이 같은 프레임에 정확히 동기화됨
            // (Physics2D.autoSyncTransforms가 기본 true이므로 transform.position도 즉시 반영됨)
            rb2D.position += (Vector2)offset;

            // 순간이동 순간 남아있던 속도는 그대로 유지되어야 하므로 velocity는 건드리지 않음
        }
        else
        {
            transform.position += offset;
        }

        if (autoClearTrail && trail != null)
        {
            // 이전 위치 -> 새 위치를 잇는 긴 궤적 선이 생기는 것을 방지
            trail.Clear();
        }

        if (autoClearParticles && particleSystems != null)
        {
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
                // World 시뮬레이션 스페이스 파티클은 위치가 안 따라오므로 초기화
                // (Local 스페이스라면 부모를 따라오므로 이 처리가 필요 없음)
                ps.Clear(true);
            }
        }
    }
}