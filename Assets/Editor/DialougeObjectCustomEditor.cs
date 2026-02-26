using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using Unity.VectorGraphics;
#if UNITY_EDITOR
public class AssetHandler
{
    [OnOpenAsset()]
    public static bool OpenEditor(int instanceId, int line)
    {
        DialougeData obj = EditorUtility.EntityIdToObject(instanceId) as DialougeData;
        if (obj != null)
        {
            DialougeEditorWindow.Open(obj);
            return true;
        }
        return false;
    }
}

[CustomEditor(typeof(DialougeData))]
public class DialougeObjectCustomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if(GUILayout.Button("Open Editor"))
        {
            DialougeEditorWindow.Open((DialougeData)target);
        }
        SceneView.RepaintAll();
    }
}

#endif