using System.Runtime.InteropServices;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.UIElements;

public class DialougeMultipleChoice : DialougeNode
{
    public override void initialize(string nodeName, DialougeGraphView graphView, Vector2 position)
    {
        base.initialize(nodeName, graphView, position);
        DialougeType = DialougeTypes.MultipleChoice;

        DialougeChoiceSavaData choiceData = new DialougeChoiceSavaData()
        {
            Text = "New Choice",
        };

        Choices.Add(choiceData);
    }

    public override void Draw()
    {
        base.Draw();

        Button addChoiceButton = DialougeElementUtility.CreateButton("Add Choice", () =>
        {
            DialougeChoiceSavaData choiceData = new DialougeChoiceSavaData()
            {
                Text = "New Choice",
            };

            Choices.Add(choiceData);
            Port choicePort = CreateChoicePort(choiceData);


            outputContainer.Add(choicePort);
        });
    

        mainContainer.Insert(1, addChoiceButton);

        foreach (DialougeChoiceSavaData choice in Choices)
        {
            Port choicePort = CreateChoicePort(choice);
            outputContainer.Add(choicePort);
        }
        RefreshExpandedState();
    }

    private Port CreateChoicePort(object userData)
    {
        Port choicePort = this.CreatePort();

        choicePort.userData = userData;

        DialougeChoiceSavaData choiceData = (DialougeChoiceSavaData)userData;

        Button deleteChoiceButton = DialougeElementUtility.CreateButton("X", () =>
        {
            if (Choices.Count == 1)
            {
                return;
            }
            if (choicePort.connected)
            {
                graphView.DeleteElements(choicePort.connections);

            }

            Choices.Remove(choiceData);

            graphView.RemoveElement(choicePort);
        });
        
        
        TextField choiceTextField = DialougeElementUtility.CreateTextField(choiceData.Text, null, callback =>
        {
            choiceData.Text = callback.newValue;
            choicePort.portName = callback.newValue;
        });
        choicePort.Add(choiceTextField);
        choicePort.Add(deleteChoiceButton);

        return choicePort;
    }
}
