using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DialogueTextManager : MonoBehaviour
{
    [SerializeField] private Button optionButtonPrefab;
    [SerializeField] private InputActionAsset actionsAsset; 
    [SerializeField] private string actionMapName = "UI";
    private InputAction clickAction;
    public DialougeContainerSO dialougeContainer;
    public DialougeSO currentDialouge;
    public TextMeshProUGUI dialogueText;
    
    private void Awake() 
{
        var map = actionsAsset.FindActionMap(actionMapName, true);
        clickAction = map.FindAction("Click", true);
    }

    private void OnEnable()
    {
        clickAction.Enable();
        clickAction.performed += OnClick;
        print($"Enabled click action: {clickAction.name}");    
    }
    private void OnDisable()
    {
        clickAction.Disable();
        clickAction.performed -= OnClick;
    }

    private void EnableTextClick()
    {
        clickAction.Enable();
    }
    private void DisableTextClick()
    {
        clickAction.Disable();
    }

    public void LoadData()
    {
        
    }
    private void Start()
    {
        dialogueText.text = currentDialouge.Text;
        dialogueText.enabled = false;
        DisableTextClick();
    }

    private void Update()
    {
        
    }

    private void UpdateText()
    {
        dialogueText.text = currentDialouge.Text;
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        NextDialouge();
        print($"going to dialouge: {currentDialouge.DialougeName}");
    }

    public void StartDialouge()
    {
        // display anything related to dialouge here
        dialogueText.enabled = true;
        EnableTextClick();
    }

    private void NextDialouge()
    {
        if (currentDialouge.Choices[0].NextDialouge == null)
        {
            // end of dialouge
            //probably should have a check to see if the next dialouge is the last one or not so that we can create like an end button.
            EndDialogue();
            return;
        }

        currentDialouge = currentDialouge.Choices[0].NextDialouge;
        // check if the next dialouge has multiple choices
        if(currentDialouge.Choices.Count > 1)
        {
            //disable input outside of the button.
            OnDisable(); // proably change this later
            for (int i = 0; i < currentDialouge.Choices.Count; i++)
            {
                int index = i;

                Vector2 buttonPos = new Vector2(0, -1 * (i + 1)); // Position buttons below the dialogue text
                
                Button optionButton = Instantiate(optionButtonPrefab, buttonPos, Quaternion.identity, transform.parent);
                
                optionButton.GetComponentInChildren<TextMeshProUGUI>().text = currentDialouge.Choices[i].Text + " Button";
                
                AddChoiceListener(optionButton, index);
                print(i + " index");
                optionButton.tag = "OptionButton";
            }
        }
        UpdateText();
        return;
    }

    private DialougeSO GetNextDialogue(DialougeSO dialougeSO, int choiceIndex)
    {
        print("Getting next dialouge for choice index: " + choiceIndex);
        return dialougeSO.Choices[choiceIndex].NextDialouge;
    }

    private void EndDialogue()
    {
        dialogueText.enabled = false;
    }

    private void OnChoiceSelected(int choiceIndex)
    {
        print("Choice " + choiceIndex + " selected");
        currentDialouge = GetNextDialogue(currentDialouge, choiceIndex);
        UpdateText();
        foreach (GameObject child in GameObject.FindGameObjectsWithTag("OptionButton"))
        {
            Destroy(child.gameObject);
        }
        OnEnable();
    }

    private void AddChoiceListener(Button button, int index)
    {
        button.onClick.AddListener(() => OnChoiceSelected(index));
    }


    
}
