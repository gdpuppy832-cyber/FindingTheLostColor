using UnityEngine;
using System;


public class BossCrystal : NormalMonster
{
    [Tooltip("크리스탈이 파괴될 때 발생하는 이벤트 (BossAttack이 구독해서 크리스탈 파괴 카운트를 셈)")]
    public event Action OnCrystalDestroyed;

    private bool crystalDestroyed = false;

    void Update()
    {
        if (!crystalDestroyed && IsPurified)
        {
            crystalDestroyed = true;
            OnCrystalDestroyed?.Invoke();
            Destroy(gameObject);
        }
    }

    // 크리스탈은 플레이어를 공격하지 않도록 NormalMonster의 접촉 데미지 메시지를 무효화
    // (new 키워드로 같은 이름의 메시지 함수를 새로 정의해서 부모의 동작을 덮어씀)
    private new void OnCollisionStay2D(Collision2D collision) { }
    private new void OnTriggerStay2D(Collider2D collision) { }
}