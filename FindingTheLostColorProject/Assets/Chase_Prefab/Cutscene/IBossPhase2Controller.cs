using System;

/// <summary>
/// 크리스탈 파괴 후 2페이즈 전환 연출(대화/컷씬)을 진행하기 위해
/// BossAttack.cs와 EZ_BossAttack.cs가 공통으로 구현하는 인터페이스.
///
/// BossPhase2DialogueTrigger는 이 인터페이스만 알고 있으면 되므로,
/// 인스펙터에 BossAttack이든 EZ_BossAttack이든(혹은 나중에 추가될 다른 난이도 보스 스크립트든)
/// 이 인터페이스를 구현한 스크립트라면 무엇이든 연결해서 사용할 수 있다.
/// </summary>
public interface IBossPhase2Controller
{
    /// <summary>
    /// 모든 크리스탈이 파괴되어 1페이즈가 끝났을 때 호출되는 이벤트.
    /// (보스는 이 시점부터 이동/공격이 동결된 채로 초기 위치에 고정되어 있어야 함)
    /// 대화 트리거 등 외부 스크립트가 이 이벤트를 구독해서 컷씬 연출을 시작한다.
    /// </summary>
    event Action OnPhase2Started;

    /// <summary>
    /// 2페이즈 "상태"를 발동한다 (보상 지급, BGM 전환, 콜라이더 활성화, 애니메이터 파라미터, 색채 구슬 소환 등).
    /// 이동/공격 동결은 그대로 유지되므로, 보스는 여전히 제자리에 가만히 있고 공격하지 않는다.
    /// </summary>
    void ActivatePhase2();

    /// <summary>
    /// 이동/공격 동결을 해제해서 보스가 실제로 움직이고 공격을 시작하게 한다.
    /// ActivatePhase2()가 이미 호출된 뒤에만 의미가 있다.
    /// </summary>
    void ReleasePhase2MovementFreeze();

    /// <summary>
    /// 컷씬(대화)이 완전히 끝난 뒤 호출해서, 검은 안개 등 2페이즈 연출 오브젝트가
    /// 그제서야 움직이기 시작하게 한다.
    /// </summary>
    void StartBlackFogMovement();
}