using System.Collections.Generic;
using UnityEngine;

public class DialougeNodeErrorData
{
    public DialougeError ErrorData { get; set; }
    public List<DialougeNode> Nodes { get; set; }

    public DialougeNodeErrorData()
    {
        ErrorData = new DialougeError();
        Nodes = new List<DialougeNode>();
    }

    
}
