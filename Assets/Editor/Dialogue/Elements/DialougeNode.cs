using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;
using System.Xml.Serialization;
using System;
using System.Linq;

public class DialougeNode : Node
{
    public string ID { get; set; }
    public string DialougeName {get; set;}
    public List<DialougeChoiceSavaData> Choices { get; set; }
    public string Text { get; set; }
    public DialougeTypes DialougeType { get; set; }

    protected DialougeGraphView graphView;
    

    public virtual void initialize(string nodeName,DialougeGraphView graphView,Vector2 position)
    {
        ID = Guid.NewGuid().ToString();
        this.graphView = graphView;
        DialougeName = nodeName;
        Choices = new List<DialougeChoiceSavaData>();
        Text = "New Text";
        SetPosition(new Rect(position, Vector2.zero));
    }

    public virtual void Draw()
    {
        TextField dialougeNameTextField = DialougeElementUtility.CreateTextField(DialougeName, label: null, callback =>
        {
            TextField target = (TextField)callback.target;
            target.value = callback.newValue;     
            
            graphView.RemoveUngroupedNode(this);

            DialougeName = callback.newValue;
            if (string.IsNullOrEmpty(target.value))
            {
                if (!string.IsNullOrEmpty(DialougeName))
                {
                    graphView.RepeatedNamesAmount++;
                }
            }
            else if(string.IsNullOrEmpty(callback.previousValue))
            {
                graphView.RepeatedNamesAmount--;
            }

            graphView.AddUngroupedNode(this);


        });
        
        titleContainer.Insert(0, dialougeNameTextField);

        Port inputPort = this.CreatePort("Dialouge Connection", Orientation.Horizontal, Direction.Input, Port.Capacity.Multi);

        inputContainer.Add(inputPort);

        VisualElement customDataContainer = new VisualElement();

        Foldout textFoldout = DialougeElementUtility.CreateFoldout("Dialouge Text");
        TextField textTextField = DialougeElementUtility.CreateTextArea(Text, null, callback =>
        {
            Text = callback.newValue;
        });
        textFoldout.Add(textTextField);
        customDataContainer.Add(textFoldout);

        extensionContainer.Add(customDataContainer);
    }

    public void DisconnectAllPorts()
    {
        DisconnectPorts(inputContainer);
        DisconnectPorts(outputContainer);
    }

    private void DisconnectPorts(VisualElement container)
    {
        foreach (Port port in container.Children())
        {
            if (!port.connected)
            {
                continue;
            }

            graphView.DeleteElements(port.connections);
        }
    }

    public bool IsStaringNode()
    {
        Port inputPort = (Port)inputContainer.Children().First();
        return !inputPort.connected;
    }

    public void SetErrorStyle(Color color)
    {
        mainContainer.style.backgroundColor = color;
    }

    public void ResetStyle()
    {
        mainContainer.style.backgroundColor = new Color(29f / 255f, 29f / 255f, 30f / 255f);
    }

}
