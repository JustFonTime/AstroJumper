using System;
using System.Collections.Generic;
using Codice.Client.BaseCommands.Merge.IncomingChanges;
using PlasticPipe.PlasticProtocol.Messages;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
public class DialougeGraphView : GraphView
{
    public DialougeEditorWindow editorWindow;
    private int repeatedNamesAmount;
    public int RepeatedNamesAmount
    {
        get 
        {
            return repeatedNamesAmount;
        }
        
        set 
        {
            repeatedNamesAmount = value;

            if (repeatedNamesAmount == 0)
            {
                editorWindow.EnableSaving();
            }

            if (repeatedNamesAmount == 1)
            {
                editorWindow.DisableButton();
            }
        }
    }
    private SerializableDictionary<string, DialougeNodeErrorData> ungroupedNodes;
    public DialougeGraphView(DialougeEditorWindow editorWindow)
    {
        this.editorWindow = editorWindow;
        AddManipulators();
        AddGridBackground();

        ungroupedNodes = new SerializableDictionary<string, DialougeNodeErrorData>();

        OnElementsDeleted();
        OnGraphViewChanged();

        AddStyles();
    }

    private void AddGridBackground()
    {
        GridBackground grid = new GridBackground();

        grid.StretchToParentSize();
        Insert(0, grid);
    }

    private void AddStyles()
    {
        StyleSheet styleSheet = (StyleSheet)EditorGUIUtility.Load("Dialouge System/DialougeGraphViewStyles.uss"); 
        styleSheets.Add(styleSheet);
    }

    private void AddManipulators()
    {
        this.AddManipulator(new ContentDragger());
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        this.AddManipulator(CreateNodeContextualMenu("Add Node (Single Choice)", DialougeTypes.SingleChoice));
        this.AddManipulator(CreateNodeContextualMenu("Add Node (Multiple Choice)", DialougeTypes.MultipleChoice));
    }

    private IManipulator CreateNodeContextualMenu(string actionTitle, DialougeTypes type)
    {
        ContextualMenuManipulator contextualMenuManipulator = new ContextualMenuManipulator(
            menuEvent => menuEvent.menu.AppendAction(actionTitle, actionEvent => CreateNode("DialougeName", type, actionEvent.eventInfo.localMousePosition)));

        return contextualMenuManipulator;
    }

    public DialougeNode CreateNode(string nodeName, DialougeTypes type, Vector2 position, bool shouldDraw = true)
    {
        Type nodeType = Type.GetType($"Dialouge{type}");
        DialougeNode node = (DialougeNode)Activator.CreateInstance(nodeType);   

        node.initialize(nodeName, this, position);
        if (shouldDraw)
        {
            node.Draw();
        }

        AddUngroupedNode(node);
        AddElement(node);
        
        return node;
    }

    public void AddUngroupedNode(DialougeNode node)
    {
        string nodeName = node.DialougeName;

        if (!ungroupedNodes.ContainsKey(nodeName))
        {
            DialougeNodeErrorData errorData= new DialougeNodeErrorData();
            errorData.Nodes.Add(node);

            ungroupedNodes.Add(nodeName, errorData);

            return;
        }

        ungroupedNodes[nodeName].Nodes.Add(node);

        Color errorColor = ungroupedNodes[nodeName].ErrorData.Color;

        node.SetErrorStyle(errorColor);

        if (ungroupedNodes[nodeName].Nodes.Count == 2)
        {
            RepeatedNamesAmount++;
            ungroupedNodes[nodeName].Nodes[0].SetErrorStyle(errorColor);
        }
    }

    public void RemoveUngroupedNode(DialougeNode node)
    {
        string nodeName = node.DialougeName;

        ungroupedNodes[nodeName].Nodes.Remove(node);

        node.ResetStyle();

        if (ungroupedNodes[nodeName].Nodes.Count == 1)
        {
            RepeatedNamesAmount--;
            ungroupedNodes[nodeName].Nodes[0].ResetStyle();   
        }

        if (ungroupedNodes[nodeName].Nodes.Count == 0)
        {
            ungroupedNodes.Remove(nodeName);
        }   


    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePort = new List<Port>();

        ports.ForEach(port =>
        {
            if(startPort == port)
            {
                return;
            } 
            if(startPort.node == port.node)
            {
                return;
            }
            
            if (startPort.direction == port.direction)
            {
                return;
            }

            compatiblePort.Add(port);
        }); 

        return compatiblePort;
    }

    private void OnElementsDeleted()
    {
        deleteSelection = (operationName, askUser) =>
        {
            Type edgeType = typeof(UnityEditor.Experimental.GraphView.Edge);

            List<DialougeNode> nodesToDelete = new List<DialougeNode>();
            List<UnityEditor.Experimental.GraphView.Edge> edgesToDelete = new List<UnityEditor.Experimental.GraphView.Edge>();

            foreach (GraphElement element in selection)
            {
                if (element is DialougeNode)
                {
                    nodesToDelete.Add((DialougeNode)element);

                    continue;
                }

                if (element.GetType() == edgeType)
                {
                    UnityEditor.Experimental.GraphView.Edge edge = (UnityEditor.Experimental.GraphView.Edge)element;
                    edgesToDelete.Add(edge);

                    continue;
                }
            }   


            foreach (DialougeNode node in nodesToDelete)
            {
                RemoveUngroupedNode(node);

                node.DisconnectAllPorts();

                RemoveElement(node);
            }

            DeleteElements(edgesToDelete);
        };
    }

    private void OnGraphViewChanged()
    {
        graphViewChanged = (changes) =>
        {
            if (changes.edgesToCreate != null)
            {
                foreach (UnityEditor.Experimental.GraphView.Edge edge in changes.edgesToCreate)
                {
                    DialougeNode nextNode = (DialougeNode)edge.input.node;
                    DialougeChoiceSavaData choiceData = (DialougeChoiceSavaData)edge.output.userData; 
                    choiceData.NodeID = nextNode.ID;
                }
            }

            if (changes.elementsToRemove != null)
            {
                Type edgeType = typeof(UnityEditor.Experimental.GraphView.Edge);

                foreach (GraphElement element in changes.elementsToRemove)
                {
                    if (element.GetType() != edgeType)
                    {
                        continue;
                    }
                    UnityEditor.Experimental.GraphView.Edge edge = (UnityEditor.Experimental.GraphView.Edge)element;
                    DialougeChoiceSavaData choiceData = (DialougeChoiceSavaData)edge.output.userData;
                    choiceData.NodeID = "";
                }   
            }
            return changes;
        };
    }

    public void ClearGraph()
    {
        graphElements.ForEach(graphElement => RemoveElement(graphElement));
        ungroupedNodes.Clear();
        RepeatedNamesAmount = 0;
    }
}
