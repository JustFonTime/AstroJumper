using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;


[RequireComponent(typeof(TextMeshProUGUI))]
public class DialogueTextManager : MonoBehaviour
{
    public static DialogueTextManager Instance { get; private set; }
    [SerializeField] private Button optionButtonPrefab;
    [SerializeField] private InputActionAsset actionsAsset; 
    [SerializeField] private string actionMapName = "UI";
    private InputAction clickAction;
    public DialougeContainerSO dialougeContainer;
    public DialougeSO currentDialouge;
    public TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject nameTextGO;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private SpriteRenderer characterIconSpriteRenderer;
    [SerializeField] public Vector3 offscreenPosition;
    [SerializeField] public Vector3 onscreenPosition;
    [SerializeField] private float duration = 1f;
    public GameObject TextContainer;
    public bool isInDialouge;
    
    public static event Action onDialogueStart;
    public static event Action onDialogueEnd;
    public Player player;

    private void Awake() 
    {
        var map = actionsAsset.FindActionMap(actionMapName, true);
        clickAction = map.FindAction("Click", true);

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        clickAction.Enable();
        clickAction.performed += OnClick;
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
        TextContainer.GetComponent<RectTransform>().position = offscreenPosition;
        dialogueText.enabled = false;

        //nameText = nameTextGO.GetComponent<TextMeshProUGUI>();
        nameTextGO.SetActive(true);
        nameText.enabled = false;

        
        DisableTextClick();
    }

    private void Update()
    {
        
    }

    private void UpdateText()
    {
        dialogueText.text = currentDialouge.Text;
        nameText.text = currentDialouge.CharacterName;
        characterIconSpriteRenderer.sprite = currentDialouge.CharacterIcon;
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        NextDialouge();
        //print($"going to dialouge: {currentDialouge.DialougeName}");
    }

    public void StartDialouge()
    {
        // display anything related to dialouge here
        dialogueText.enabled = true;
        dialogueText.text = currentDialouge.Text;
        
        nameText.enabled = true;
        nameText.text = currentDialouge.CharacterName;
        
        characterIconSpriteRenderer.sprite = currentDialouge.CharacterIcon;

        isInDialouge = true;
        
        onDialogueStart?.Invoke();
        StartCoroutine(moveDialogueBox());
    }

    public void StartDialouge(DialougeSO dialouge)
    {
        currentDialouge = dialouge;
        StartDialouge();
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

            GameObject optionButtonTransform = GameObject.Find("ChoiceTransform");
            for (int i = 0; i < currentDialouge.Choices.Count; i++)
            {
                int index = i;

                Button optionButton = Instantiate(optionButtonPrefab, new Vector2(0,0), Quaternion.identity, transform.parent);
                optionButton.transform.SetParent(GameObject.Find("Canvas").transform, false);
                RectTransform optionButtonRect = optionButtonTransform.GetComponent<RectTransform>();
                Vector2 buttonPos = new Vector2(optionButtonRect.anchoredPosition.x, optionButtonRect.anchoredPosition.y - (i * optionButtonPrefab.GetComponent<RectTransform>().rect.height)); 
                
                optionButton.GetComponent<RectTransform>().anchoredPosition = buttonPos;
                optionButton.GetComponentInChildren<TextMeshProUGUI>().text = currentDialouge.Choices[i].Text + " Button";

                // Check if player has stats for option 
                
                
                AddChoiceListener(optionButton, index);
                optionButton.tag = "OptionButton";
            }
        }
        UpdateText();
        return;
    }

    private DialougeSO GetNextDialogue(DialougeSO dialougeSO, int choiceIndex)
    {
        return dialougeSO.Choices[choiceIndex].NextDialouge;
    }

    private void EndDialogue()
    {
        
        print("Dialogue ended" + isInDialouge);
        StartCoroutine(moveDialogueBox());
        onDialogueEnd?.Invoke();
        
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

    private IEnumerator moveDialogueBox()
    {
        float timeElapsed = 0f;
        if (TextContainer.GetComponent<RectTransform>().position == offscreenPosition)
        {
            // move it on screen
            timeElapsed = 0f;
            while (timeElapsed < duration)
            {
                TextContainer.GetComponent<RectTransform>().position = Vector3.Lerp(offscreenPosition, onscreenPosition, timeElapsed / duration);
                timeElapsed += Time.deltaTime;
                yield return null; 
            }

            TextContainer.GetComponent<RectTransform>().position = onscreenPosition; 
            EnableTextClick();
        }
        else
        {
            // move it off screen
             timeElapsed = 0f;
            while (timeElapsed < duration)
            {
                TextContainer.GetComponent<RectTransform>().position = Vector3.Lerp(onscreenPosition, offscreenPosition, timeElapsed / duration);
                timeElapsed += Time.deltaTime;
                yield return null; 
            }
            isInDialouge = false;
            TextContainer.GetComponent<RectTransform>().position = offscreenPosition; 
            DisableTextClick();
        }
    }

    
}

#if UNITY_EDITOR
[CustomEditor(typeof(DialogueTextManager))]
public class DialogueTextManagerInspector : Editor 
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        if(GUILayout.Button("Set OffScreen Position"))
        {
            DialogueTextManager manager = (DialogueTextManager)target;
            Undo.RecordObject(manager, "Set OffScreen Position");
            manager.offscreenPosition = manager.TextContainer.GetComponent<RectTransform>().position;

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
        if(GUILayout.Button("Set OnScreen Position"))
        {
            DialogueTextManager manager = (DialogueTextManager)target;
            Undo.RecordObject(manager, "Set OnScreen Position");
            manager.onscreenPosition = manager.TextContainer.GetComponent<RectTransform>().position;

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }
}

#endif 