using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public interface IDebugTool {
    void SetText(int channel, string text);
}

public class DebugTool : MonoBehaviour, IDebugTool {
    public List<Text> Outputs = new List<Text>();

    public static IDebugTool current = new NullTool();

    // Use this for initialization
    void Start() {
        current = this;
    }

    public void SetText(int channel, string text) {
        if (channel < 0 || channel >= Outputs.Count)
            return;
        Outputs[channel].text = text;
    }

    private class NullTool : IDebugTool {
        public void SetText(int channel, string text) { }
    }
}