using TMPro;
using UnityEngine;

public static class DialogueManager
{
    public static DialougeSO LoadDialouge(DialougeContainerSO dialougeContainer, TextMeshProUGUI dialogueText)
    {
        DialougeSO currentDialouge = dialougeContainer.Dialouges[0];
        dialogueText.text = currentDialouge.Text;
        return currentDialouge;
    }

    public static void StartDialouge()
    {
        
    }

}
