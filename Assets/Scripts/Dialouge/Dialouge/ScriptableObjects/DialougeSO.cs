using System.Collections.Generic;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;

public class DialougeSO : ScriptableObject
{
    [field: SerializeField] public string DialougeName { get; set; }
    [field: SerializeField] public string CharacterName { get; set; }
    [field: SerializeField] public Sprite CharacterIcon { get; set; }
    [field: SerializeField] public string Text { get; set; }
    [field: SerializeField] public List<DialougeChoiceData> Choices { get; set; }
    [field: SerializeField] public DialougeTypes DialougeTypes { get; set; }
    [field: SerializeField] public bool IsStartingDialouge { get; set; }

    public void Initialize(string dialougeName, string text, string characterName, Sprite characterIcon, List<DialougeChoiceData> choices, DialougeTypes dialougeTypes, bool isStartingDialouge)
    {
        DialougeName = dialougeName;
        CharacterName = characterName;
        CharacterIcon = characterIcon;
        Text = text;
        Choices = choices;
        DialougeTypes = dialougeTypes;
        IsStartingDialouge = isStartingDialouge;
    }
}
