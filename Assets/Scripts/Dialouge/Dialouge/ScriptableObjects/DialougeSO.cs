using System.Collections.Generic;
using UnityEngine;

public class DialougeSO : ScriptableObject
{
    [field: SerializeField] public string DialougeName { get; set; }
    [field: SerializeField] public string CharacterNameKey { get; set; }
    [field: SerializeField] public Sprite CharacterIcon { get; set; }
    [field: SerializeField] public string TextKey { get; set; }
    [field: SerializeField] public List<DialougeChoiceData> Choices { get; set; }
    [field: SerializeField] public DialougeTypes DialougeTypes { get; set; }
    [field: SerializeField] public bool IsStartingDialouge { get; set; }

    public void Initialize(string dialougeName, string textKey, string characterNameKey, Sprite characterIcon, List<DialougeChoiceData> choices, DialougeTypes dialougeTypes, bool isStartingDialouge)
    {
        DialougeName = dialougeName;
        CharacterNameKey = characterNameKey;
        CharacterIcon = characterIcon;
        TextKey = textKey;
        Choices = choices;
        DialougeTypes = dialougeTypes;
        IsStartingDialouge = isStartingDialouge;
    }
}
