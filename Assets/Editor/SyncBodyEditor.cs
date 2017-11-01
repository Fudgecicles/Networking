using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SyncBody))]
public class SyncBodyEditor : Editor{

    SyncBody script;

    private void OnEnable()
    {
        script = target as SyncBody;
    }

    public override void OnInspectorGUI()
    {
        script.fixedUpdatesPerSend = EditorGUILayout.IntSlider("Fixed Updates Per Packet", script.fixedUpdatesPerSend, 1, (int)(1 / Time.fixedDeltaTime));
        EditorGUILayout.LabelField((((int)(1 / Time.fixedDeltaTime)) / script.fixedUpdatesPerSend).ToString() + " Packets per second");
    }

}
