using UnityEngine;

public class Dialoguetrigger : MonoBehaviour
{
    public DialougeSO dialogue;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            DialogueTextManager.Instance.currentDialouge = dialogue;
            DialogueTextManager.Instance.StartDialouge();
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
