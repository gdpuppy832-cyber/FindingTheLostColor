using UnityEngine;
using System;
public class BossCrystal : NormalMonster
{
    [Tooltip("크리스탈이 파괴될 때 발생하는 이벤트 (BossAttack이 구독해서 크리스탈 파괴 카운트를 셈)")]
    public event Action OnCrystalDestroyed;
    [Tooltip("크리스탈이 파괴되는 순간 재생할 효과음")]
    public AudioClip crystalDestroySFX;
    private bool crystalDestroyed = false;
    private bool halfHealthTriggered = false; // ★ 체력 50% 파라미터 중복 발동 방지
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // ★ 체력이 최대 체력의 절반 이상이 되는 순간 "50" 파라미터를 한 번만 발동
        if (!halfHealthTriggered && currentHealth >= maxHealth * 0.5f)
        {
            halfHealthTriggered = true;
            if (animator != null)
            {
                animator.SetTrigger("50");
            }
        }

        if (!crystalDestroyed && IsPurified)
        {
            crystalDestroyed = true;
            // 사운드가 오브젝트와 함께 즉시 사라지지 않도록,
            // 오브젝트 파괴 위치에 임시 재생용 오브젝트를 하나 만들어 그쪽에서 소리를 재생함
            if (crystalDestroySFX != null)
            {
                AudioSource.PlayClipAtPoint(crystalDestroySFX, transform.position);
            }

            // ★ 파괴 애니메이션 트리거 발동 (오브젝트 자체는 파괴하지 않음)
            if (animator != null)
            {
                animator.SetTrigger("Destroy");
            }

            OnCrystalDestroyed?.Invoke();
        }
    }
}