using UnityEngine;

public class HighlightMyText : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip; 
    [SerializeField] private Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    public void HighlightUIText()
    {
        animator.SetTrigger("TriggerHighlightUI");
        if (audioClip != null)
            AudioManager.Instance.PlayLocalSFX(audioClip);
    }
}
