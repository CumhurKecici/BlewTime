using UnityEngine;

public class PowerupController : MonoBehaviour, IPowerUp
{
    public PowerupType powerupType;

    private Animator m_animator;
    private Animation m_animation;


    void Start()
    {
        // m_animator = GetComponent<Animator>();
    }

    void Update()
    {


        /* if (GameManager.Instance.IsPlayMode)
             m_animator.speed = 1;
         else
             m_animator.speed = 0;*/
    }
}

public enum PowerupType
{
    ExtraBomb,
    Speed,
    ExtraRange,
    ExtraLife
}