using System.Collections.Generic;
using UnityEngine;

public class DialougeContainerSO : ScriptableObject
{
    [field: SerializeField] public string FileName { get; set; }
    [field: SerializeField] public List<DialougeSO> Dialouges { get; set; }

    public void Initialize(string fileName)
    {
        FileName = fileName;
        Dialouges = new List<DialougeSO>();
    }
}
