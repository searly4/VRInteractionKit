using UnityEngine;
using System.Collections;
using System;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

namespace VRIK {
    public class ControllerModeHighlighter : MonoBehaviour {
        [SerializeField]
        public ModeHighlightList colorMapping = new ModeHighlightList();

        public Renderer tipRenderer;

        VRController_SteamVR controller;

        static readonly string[] materialColors = { "_Color", "_EmissionColor", "_SpecColor", "_ReflectColor", "_EmisColor" };

        void OnEnable() {
            controller = GetComponent<VRController_SteamVR>();
            controller.ModeChanged += OnModeChanged;
        }

        void OnDisable() {
            controller.ModeChanged -= OnModeChanged;
            controller = null;
        }

        private void OnModeChanged(object source, int oldMode, int newMode) {
            foreach (var mapping in colorMapping.list) {
                if (((1 << newMode) & mapping.modeMask) == 0)
                    continue;

                foreach (string colorName in materialColors) {
                    tipRenderer.material.SetColor(colorName, mapping.color);
                }
                return;
            }
        }


        [Serializable]
        public class ModeHighlightList {
            [SerializeField]
            public List<ModeHighlightMapping> list = new List<ModeHighlightMapping>();

            [NonSerialized]
            public IButtonSource buttonSource;

            public ModeHighlightMapping this[int i] {
                get { return list[i]; }
                set { list[i] = value; }
            }

            public interface IButtonSource {
                string[] names { get; }
                int[] values { get; }
                int this[string name] { get; }
            }
        }

        [Serializable]
        public class ModeHighlightMapping {
            public int modeMask;
            public Color color;
        }

    }


    [CustomPropertyDrawer(typeof(ControllerModeHighlighter.ModeHighlightList))]
    public class ModeHighlightListPropertyDrawer : PropertyDrawer {
        ReorderableList list;
        ControllerModeHighlighter.ModeHighlightList realproperty;
        string[] modeNames;
        Vector2 labelSize;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Modes");
        static readonly GUIContent ColorLabel = new GUIContent("Color");

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                realproperty = (ControllerModeHighlighter.ModeHighlightList)fieldInfo.GetValue(property.serializedObject.targetObject);
                list = new ReorderableList(realproperty.list, typeof(ControllerModeHighlighter.ModeHighlightMapping), true, true, true, true);
                //list = new ReorderableList(property.serializedObject, property, true, true, true, true);

                list.drawElementCallback = DrawMapping;
                list.elementHeight = 16 * 1 + 4;
                //list.headerHeight = 32;
                list.draggable = false;

                list.drawHeaderCallback = (Rect rect) => {
                    EditorGUI.LabelField(rect, "Button Mappings");
                };
                /*list.elementHeightCallback = (int index) => {
                    return (index == 0) ? 36 : 18;
                };//*/
            }
            return list;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            realproperty = (ControllerModeHighlighter.ModeHighlightList)fieldInfo.GetValue(property.serializedObject.targetObject);

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            labelSize = GUI.skin.label.CalcSize(ColorLabel);

            getList(property);

            EditorGUI.BeginProperty(position, label, property);
            property.serializedObject.Update();
            //list.DoLayoutList();
            list.DoList(position);
            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return getList(property).GetHeight();
        }

        void DrawMapping(Rect rect, int index, bool isActive, bool isFocused) {
            rect.height = 16;
            ControllerModeHighlighter.ModeHighlightMapping mapping = realproperty[index];
            //var element = list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            Vector2 spacerSize = new Vector2(labelSize.x / 2, rect.height);
            Vector2 selectorSize = new Vector2((rect.width - (labelSize.x * 2 + spacerSize.x)) / 2, rect.height);
            Vector2 position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ModeSelectLabel);
            position.x += labelSize.x;
            mapping.modeMask = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.modeMask, modeNames);
            position.x += selectorSize.x + spacerSize.x;
            EditorGUI.LabelField(new Rect(position, labelSize), ColorLabel);
            position.x += labelSize.x;
            mapping.color = EditorGUI.ColorField(new Rect(position, selectorSize), mapping.color);
        }
    }//*/
}