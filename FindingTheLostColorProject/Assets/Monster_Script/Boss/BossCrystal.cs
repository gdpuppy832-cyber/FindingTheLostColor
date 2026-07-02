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
}