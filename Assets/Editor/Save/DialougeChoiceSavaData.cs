using System;
using UnityEngine;

[Serializable]
public class DialougeChoiceSavaData 
{
    [field: SerializeField] public string Text { get; set; }
    [field: SerializeField] public string NodeID { get; set; }
}
