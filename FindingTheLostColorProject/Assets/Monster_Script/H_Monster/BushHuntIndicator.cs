using UnityEngine;
public class BushHuntIndicator : MonoBehaviour
{
    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // 오브젝트가 켜질 때마다(재사용 포함) 애니메이터를 확실히 기본 상태로 되돌려서,
    // 이전에 멈추거나 어중간한 프레임에 걸려있던 상태로 재사용되는 것을 방지
    void OnEnable()
    {
        if (animator == null) return;

        animator.Rebind();
        animator.Update(0f);
    }

    public void SetHunting(bool hunting)
    {
        if (animator != null)
            animator.SetBool("IsHunting", hunting);
    }

    public void SetInPlayer()
    {
        if (animator != null)
            animator.SetTrigger("InPlayer");
    }
}