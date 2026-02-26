using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
public class DialougeChoiceData 
{
    [field: SerializeField] public string Name { get; set; }
    [field: SerializeField] public string Text { get; set; }
    [field: SerializeField] public DialougeSO NextDialouge { get; set; }    
}
