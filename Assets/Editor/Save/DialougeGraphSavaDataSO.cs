using System.Collections.Generic;
using UnityEngine;

public class DialougeGraphSavaDataSO : ScriptableObject
{
    [field: SerializeField] public string FileName { get; set; }
    [field: SerializeField] public List<DialougeNodeSavaData> Nodes { get; set; }
    [field: SerializeField] public List<string> OldUngroupedNodeNames { get; set; }

    public void Initialize(string fileName)
    {
        FileName = fileName;
        Nodes = new List<DialougeNodeSavaData>();
    }

}
