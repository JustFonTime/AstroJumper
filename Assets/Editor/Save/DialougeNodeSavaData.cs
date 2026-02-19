using System;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;
using UnityEngine;

[Serializable]
public class DialougeNodeSavaData 
{
    [field: SerializeField] public string Name { get; set; }
    [field: SerializeField] public string Text { get; set; }
    [field: SerializeField] public string ID { get; set; }
    [field: SerializeField] public List<DialougeChoiceSavaData> Choices { get; set; }
    [field: SerializeField] public DialougeTypes DialougeType { get; set; }
    [field: SerializeField] public Vector2 Position { get; set; }
}
