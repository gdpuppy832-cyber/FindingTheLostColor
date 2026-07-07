using UnityEngine;
public class ContactHit : MonoBehaviour
{
    public System.Action<Collider2D> onTriggerEnter;
    public System.Action<Collider2D> onTriggerStay;
    void OnTriggerEnter2D(Collider2D other)
    {
        
        onTriggerEnter?.Invoke(other);
    }
    void OnTriggerStay2D(Collider2D other)
    {
        onTriggerStay?.Invoke(other);
    }
}