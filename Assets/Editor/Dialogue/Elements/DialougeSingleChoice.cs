using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class DialougeSingleChoice : DialougeNode
{
    public override void initialize(string nodeName, DialougeGraphView graphView, Vector2 position)
    {
        base.initialize(nodeName, graphView, position);
        DialougeType = DialougeTypes.SingleChoice;

        DialougeChoiceSavaData choiceData = new DialougeChoiceSavaData()
        {
            Text = "Next Dialouge",
        };

        Choices.Add(choiceData);
    }

    public override void Draw()
    {
        base.Draw();

        foreach (DialougeChoiceSavaData choice in Choices)
        {
            Port choicePort = this.CreatePort(choice.Text, Orientation.Horizontal, Direction.Output, Port.Capacity.Multi);

            choicePort.userData = choice;
            outputContainer.Add(choicePort);
        }
        RefreshExpandedState();
    }
}
