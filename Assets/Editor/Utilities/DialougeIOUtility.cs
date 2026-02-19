using System;
using System.Collections.Generic;
using System.Linq;
using Codice.Client.BaseCommands;
using Codice.Client.BaseCommands.WkStatus.Printers;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public static class DialougeIOUtility
{
    private static DialougeGraphView graphView;
    private static string graphFileName;
    private static string containerFolderPath;
    private static List<DialougeNode> nodes;
    private static Dictionary<string, DialougeSO> createDialouges;
    private static Dictionary<string, DialougeNode> loadedNodes;
    public static void Initialize(DialougeGraphView dsGraphView, string graphName)
    {
        graphFileName = graphName;
        DialougeIOUtility.graphView = dsGraphView;
        containerFolderPath = $"Assets/Dialouge/Dialouges/{graphFileName}";
        nodes = new List<DialougeNode>();
        createDialouges = new Dictionary<string, DialougeSO>();
        loadedNodes = new Dictionary<string, DialougeNode>();

    }
    public static void Save()
    {
        CreateStaticFolders();

        GetElementsFromGraphView();

        DialougeGraphSavaDataSO graphData = CreateAsset<DialougeGraphSavaDataSO>("Assets/Editor/Dialogue/Graphs", $"{graphFileName}GraphData");
   
        graphData.Initialize(graphFileName);

        DialougeContainerSO dialougeContainer = CreateAsset<DialougeContainerSO>(containerFolderPath, graphFileName);
        dialougeContainer.Initialize(graphFileName);

        SaveNodes(graphData, dialougeContainer);

        SaveAsset(graphData);
        SaveAsset(dialougeContainer);
    }

    public static void Load()
    {
        DialougeGraphSavaDataSO graphData = LoadAsset<DialougeGraphSavaDataSO>("Assets/Editor/Dialogue/Graphs", graphFileName);

        if (graphData == null)
        {
            EditorUtility.DisplayDialog("Couldn't load the file",
             "The file at path could not be found \n\n." + $"Assets/Editor/Dialogue/Graphs/{graphFileName}",
             "OK");
            return;
        }

        DialougeEditorWindow.UpdateFileName(graphData.FileName);
        LoadNodes(graphData.Nodes);
        LoadNodesConnections();
    }

    private static T LoadAsset<T>(string path, string assetName) where T : UnityEngine.Object
    {
        string fullPath = $"{path}/{assetName}.asset";
        return AssetDatabase.LoadAssetAtPath<T>(fullPath);
    }

    private static void RemoveAsset(string path, string assetName)
    {
        string fullPath = $"{path}/{assetName}.asset";
        AssetDatabase.DeleteAsset(fullPath);
    }

    private static T CreateAsset<T>(string path, string assetName) where T : ScriptableObject
    {
        string fullPath = $"{path}/{assetName}.asset";
        T asset = LoadAsset<T>(path, assetName);

        if(asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();

            AssetDatabase.CreateAsset(asset, fullPath);
        }

        return asset;
    }

    private static void LoadNodes(List<DialougeNodeSavaData> nodes)
    {
        foreach (DialougeNodeSavaData nodeData in nodes)
        {
            List<DialougeChoiceSavaData> choices = CloneNodeChoices(nodeData.Choices);

            DialougeNode node = graphView.CreateNode(nodeData.Name, nodeData.DialougeType, nodeData.Position, false);

            node.ID = nodeData.ID;
            node.Text = nodeData.Text;
            node.Choices = choices;

            node.Draw();

            graphView.AddElement(node);
            loadedNodes.Add(node.ID, node);
        }
    }

    private static void LoadNodesConnections()
    {
        foreach (KeyValuePair<string, DialougeNode> loadedNode in loadedNodes)
        {
            foreach (Port choicePort in loadedNode.Value.outputContainer.Children())
            {
                DialougeChoiceSavaData choiceData = (DialougeChoiceSavaData)choicePort.userData;

                if (string.IsNullOrEmpty(choiceData.NodeID))
                {
                    continue;
                }

                DialougeNode nextNode = loadedNodes[choiceData.NodeID]; 

                Port nextNodeInputPort = (Port)nextNode.inputContainer.Children().First();        

                UnityEditor.Experimental.GraphView.Edge edge = choicePort.ConnectTo(nextNodeInputPort);
                graphView.AddElement(edge);

                loadedNode.Value.RefreshPorts();
            }
        }
    }

    private static void GetElementsFromGraphView()
    {
        graphView.graphElements.ForEach(graphElement =>
        {
            if (graphElement is DialougeNode dialougeNode)
            {
                nodes.Add(dialougeNode);
                return;
            }
        });
    }

    private static void CreateStaticFolders()
    {
        CreateFolder("Assets/Editor/Dialogue", "Graphs");
        CreateFolder("Assets", "Dialouge");
        CreateFolder("Assets/Dialouge", "Dialouges");   

        CreateFolder("Assets/Dialouge/Dialouges", graphFileName);
        CreateFolder(containerFolderPath, "Global");
        CreateFolder($"{containerFolderPath}/Global", "Dialouges");    
    }

    private static void RemoveFolder(string path)
    {
        FileUtil.DeleteFileOrDirectory($"{path}.meta+");
        FileUtil.DeleteFileOrDirectory($"{path}/");
    }
    private static void CreateFolder(string path, string folderName)
    {
        if (AssetDatabase.IsValidFolder($"{path}/{folderName}"))
        {
            return;
        }

        AssetDatabase.CreateFolder(path, folderName);
    }

    private static void SaveAsset(UnityEngine.Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        AssetDatabase.Refresh();
    }

    private static void SaveNodes(DialougeGraphSavaDataSO graphData, DialougeContainerSO dialougeContainer)
    {
        List<string> ungroupedNodeNames = new List<string>();
        foreach (DialougeNode node in nodes)
        {
            SaveNodeToGraph(node, graphData);
            SaveNodeToSO(node, dialougeContainer);

            ungroupedNodeNames.Add(node.DialougeName);
        }

        UpdateDialougeChoicesConnections();

        updateOldUngroupedNodes(ungroupedNodeNames, graphData);

    }

    private static void updateOldUngroupedNodes(List<string> currentNodeNames, DialougeGraphSavaDataSO graphData)
    {
        if (graphData.OldUngroupedNodeNames != null && graphData.OldUngroupedNodeNames.Count != 0)
        {
            List<string> nodesToRemove = graphData.OldUngroupedNodeNames.Except(currentNodeNames).ToList();

            foreach (string nodeName in nodesToRemove)
            {
                RemoveAsset($"{containerFolderPath}/Global/Dialouges", nodeName);
            }
        }

        graphData.OldUngroupedNodeNames = new List<string>(currentNodeNames);
    }

    private static void UpdateDialougeChoicesConnections()
    {
        foreach (DialougeNode node in nodes)
        {   
            DialougeSO dialouge = createDialouges[node.ID];
            for (int choiceIndex = 0; choiceIndex < node.Choices.Count; choiceIndex++)
            {
                DialougeChoiceSavaData nodeChoice = node.Choices[choiceIndex];

                if (string.IsNullOrEmpty(nodeChoice.NodeID))
                {
                    Debug.Log("No connection for choice: " + nodeChoice.Text);
                    continue;
                }

                
                dialouge.Choices[choiceIndex].NextDialouge = createDialouges[nodeChoice.NodeID];
                
                SaveAsset(dialouge);
            }
        }
    }

    private static void SaveNodeToSO(DialougeNode node, DialougeContainerSO dialougeContainer)
    {
        DialougeSO dialouge;
        dialouge = CreateAsset<DialougeSO>($"{containerFolderPath}/Global/Dialouges", node.DialougeName);

        dialougeContainer.Dialouges.Add(dialouge);

        dialouge.Initialize(node.DialougeName, node.Text, ConvertNodeChoicesToDialougeChoices(node.Choices), node.DialougeType, node.IsStaringNode());

        Debug.Log(dialouge.Choices.Count);
        if(ConvertNodeChoicesToDialougeChoices(node.Choices) == null)
        {
            Debug.Log("No choices to convert for node: " + node.DialougeName);
        }
        createDialouges.Add(node.ID, dialouge);

        SaveAsset(dialouge);
    }

    private static List<DialougeChoiceData> ConvertNodeChoicesToDialougeChoices(List<DialougeChoiceSavaData> nodeChoices)
    {
        List<DialougeChoiceData> dialougeChoices = new List<DialougeChoiceData>();

        foreach (DialougeChoiceSavaData nodeChoice in nodeChoices)
        {
            DialougeChoiceData choiceData = new DialougeChoiceData()
            {
                Text = nodeChoice.Text,
            };
        
            dialougeChoices.Add(choiceData);
        }
        return dialougeChoices;
    }

    private static List<DialougeChoiceSavaData> CloneNodeChoices(List<DialougeChoiceSavaData> nodeChoices)
    {
        List<DialougeChoiceSavaData> choices = new List<DialougeChoiceSavaData>();

        foreach (DialougeChoiceSavaData choice in nodeChoices)
        {
            DialougeChoiceSavaData choiceData = new DialougeChoiceSavaData()
            {
                Text = choice.Text,
                NodeID = choice.NodeID,
            };

            choices.Add(choiceData);
        }
        return choices;
    }

    private static void SaveNodeToGraph(DialougeNode node, DialougeGraphSavaDataSO graphData)
    {
        List<DialougeChoiceSavaData> choices = CloneNodeChoices(node.Choices);

        DialougeNodeSavaData nodeSavaData = new DialougeNodeSavaData()
        {
            ID = node.ID,
            Name = node.DialougeName,
            Choices = choices,
            Text = node.Text,
            DialougeType = node.DialougeType,
            Position = node.GetPosition().position,
        };

        graphData.Nodes.Add(nodeSavaData);
    }


}
