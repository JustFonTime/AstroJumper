using System.Collections.Generic;
using UnityEngine;

public class DialougeSO : ScriptableObject
{
    [field: SerializeField] public string DialougeName { get; set; }
    [field: SerializeField] public string Text { get; set; }
    [field: SerializeField] public List<DialougeChoiceData> Choices { get; set; }
    [field: SerializeField] public DialougeTypes DialougeTypes { get; set; }
    [field: SerializeField] public bool IsStartingDialouge { get; set; }

    public void Initialize(string dialougeName, string text, List<DialougeChoiceData> choices, DialougeTypes dialougeTypes, bool isStartingDialouge)
    {
        DialougeName = dialougeName;
        Text = text;
        Choices = choices;
        DialougeTypes = dialougeTypes;
        IsStartingDialouge = isStartingDialouge;
    }
}
