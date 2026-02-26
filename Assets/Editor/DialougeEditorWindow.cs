using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Drawing.Printing;
using UnityEditor.UIElements;
using UnityEditor.PackageManager.Requests;
using PlasticGui;
using Codice.Client.BaseCommands;
using System.IO;
public class DialougeEditorWindow : EditorWindow
{
    private static TextField fileNameTextField;
    private DialougeGraphView graphView;
    private readonly string defaultFileName = "DialougeFileName";
    private Button saveButton;
    public static void Open(DialougeData dataObject)
    {
        Debug.Log("Opening Editor Window");
        DialougeEditorWindow window = GetWindow<DialougeEditorWindow>("Dialouge Editor");
    }
    
    private void CreateGUI()
    {
        AddGraphView();
        AddToolBar();

        AddStyles();
    }   

    private void AddGraphView()
    {
        graphView = new DialougeGraphView(this);
        graphView.StretchToParentSize();

        rootVisualElement.Add(graphView);
    }

    private void AddToolBar()
    {
        Toolbar toolbar = new Toolbar();

        fileNameTextField = DialougeElementUtility.CreateTextField(defaultFileName, "File Name:", null);

        saveButton = DialougeElementUtility.CreateButton("Save", () =>
        {
            Save(fileNameTextField);
        });

        Button loadButton = DialougeElementUtility.CreateButton("Load", () => Load());

        Button clearButton = DialougeElementUtility.CreateButton("Clear", () => Clear());

        toolbar.Add(fileNameTextField);
        toolbar.Add(saveButton);
        toolbar.Add(clearButton);
        toolbar.Add(loadButton);

        rootVisualElement.Add(toolbar);


    }

    public void Clear()
    {
        graphView.ClearGraph();
    }

    private void Save(TextField textField)
    {
        if (string.IsNullOrEmpty(textField.value))
        {
            EditorUtility.DisplayDialog("Invalid File Name", "Please enter a valid file name before saving.", "OK");
            return;
        }
        
        DialougeIOUtility.Initialize(graphView, textField.value);
        DialougeIOUtility.Save();

    }

    private void Load()
    {
        string path = EditorUtility.OpenFilePanel("dialouge Graphs", "Assets/Editor/Dialogue/Graphs", "asset");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        Clear();

        DialougeIOUtility.Initialize(graphView, Path.GetFileNameWithoutExtension(path));
        DialougeIOUtility.Load();
    }

    private void AddStyles()
    {
        StyleSheet styleSheet = (StyleSheet)EditorGUIUtility.Load("Dialouge System/DialougeVariables.uss"); 
        rootVisualElement.styleSheets.Add(styleSheet);
    }

    public void EnableSaving()
    {
        saveButton.SetEnabled(true);
    }

    public void DisableButton()
    {
        saveButton.SetEnabled(false);
    }

    public static void UpdateFileName(string newFileName)
    {
        fileNameTextField.value = newFileName;
    }

}
