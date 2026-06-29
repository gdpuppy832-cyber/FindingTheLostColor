using UnityEngine;
using UnityEngine.UIElements;

public class EnemyMove : MonoBehaviour
{
    public float speed = 1.5f;
    public Transform target;
    public float range;
    float timer = 0;
    public SpriteRenderer sr;
    public Rigidbody2D rd;
    public float xspeed;
    void Start()
    {
       
    }


    void Update()
    {
        float distance = Vector3.Distance(transform.position, target.position);

        timer += Time.deltaTime;
        if (distance <= range)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            transform.Translate(speed * direction * Time.deltaTime, 0f);
        }
        else if (timer < 3)
        {

            transform.Translate(new Vector2(speed * Time.deltaTime, 0f));

        }
        else if (timer < 6)
        {
            transform.Translate(new Vector2(-speed * Time.deltaTime, 0f));
        }
        else
        {
            timer = 0;
        }

        xspeed = rd.linearVelocity.x;
        if (xspeed > 0)
        {
            sr.flipX = false;
        }
        else if (xspeed < 0)
        {
            sr.flipX = true;
        }
    }
}
