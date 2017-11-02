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
        script.fixedUpdatesPerSend = EditorGUILayout.IntSlider(new GUIContent("Fixed Updates Per Packet", "How many fixed updates to wait before sending another packet"), script.fixedUpdatesPerSend, 1, (int)(1 / Time.fixedDeltaTime));
        EditorGUILayout.LabelField((((int)(1 / Time.fixedDeltaTime)) / script.fixedUpdatesPerSend).ToString() + " Packets per second");
        script.latencyDecreaseRate = EditorGUILayout.Slider(new GUIContent("Latency Decrease Rate", "The rate latency decreases for each successfuly interpolated frame without running out the buffer\n very low values recommended"), script.latencyDecreaseRate, 0,.1f);
        script.numInterpolationDelayFrames = EditorGUILayout.IntSlider(new GUIContent("Interpolation Delay Frames", "The number of frames to delay when interpolating, higher values mean smoother interpolation but more latency"), script.numInterpolationDelayFrames, 3, 7);
        script.decreaseMinLatencyFrames = EditorGUILayout.IntField(new GUIContent("Successful Frames Before Min Latency Decrease", "If we don't empty the frame buffer for this many frames our minimum latency will decrease assuming network conditions have gotten better"), script.decreaseMinLatencyFrames);
        script.decreaseMinLatencyAmount = EditorGUILayout.Slider(new GUIContent("Min Latency Decrease Amount", "Min latency decreases this much everytime we don't empty the frame buffer for a period"), script.decreaseMinLatencyAmount, 0, .2f);
        script.minDeltaPositionSendDistance = EditorGUILayout.FloatField("Min Delta Position Send Distance", script.minDeltaPositionSendDistance);
        script.stopSendIdleTimer = EditorGUILayout.FloatField("Stop send idle timer", script.stopSendIdleTimer);
    }

}
