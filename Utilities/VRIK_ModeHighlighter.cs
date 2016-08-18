using UnityEngine;
using System.Collections;
using System;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

namespace VRIK {
    public class VRIK_ModeHighlighter : MonoBehaviour {
        [SerializeField]
        public ModeHighlightList colorMapping = new ModeHighlightList();

        public bool highlightChildren = false;

        public VRInteractor interactor;

        List<ColorSet> originalObjectColours = null;

        void OnEnable() {
            if (interactor == null)
                interactor = GetComponent<VRInteractor>();
            if (interactor == null)
                return;
            interactor.ModeChanged += OnModeChanged;
        }

        void OnDisable() {
            if (interactor == null)
                return;
            interactor.ModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(object source, int oldMode, int newMode) {
            foreach (var mapping in colorMapping.list) {
                if (((1 << newMode) & mapping.modeMask) == 0)
                    continue;

                if (originalObjectColours == null || originalObjectColours.Count == 0)
                    originalObjectColours = ReplaceColors(interactor, mapping.color);
                else
                    SetColors(interactor, mapping.color);

                return;
            }

            if (originalObjectColours == null)
                return;
            RestoreColors(originalObjectColours);
            originalObjectColours = null;
        }

        void SetColors(VRInteractor interactor, Color newcolor) {
            List<Renderer> renderers = new List<Renderer>();
            if (highlightChildren)
                renderers.AddRange(interactor.GetComponentsInChildren<Renderer>());
            else
                renderers.Add(interactor.GetComponent<Renderer>());

            foreach (Renderer r in renderers) {
                if (r.material == null)
                    continue;

                // set new colors
                foreach (ColorMask c in Enum.GetValues(typeof(ColorMask))) {
                    string colorName = c.ToString();
                    if (!r.material.HasProperty(colorName))
                        continue;
                   
                    r.material.SetColor(colorName, newcolor);
                }
            }
        }

        List<ColorSet> ReplaceColors(VRInteractor interactor, Color replacecolor) {
            List<ColorSet> originalColors = new List<ColorSet>();

            List<Renderer> renderers = new List<Renderer>();
            if (highlightChildren)
                renderers.AddRange(interactor.GetComponentsInChildren<Renderer>());
            else
                renderers.Add(interactor.GetComponent<Renderer>());

            foreach (Renderer r in renderers) {
                if (r.material == null)
                    continue;

                // store old colors and set new colors
                foreach (ColorMask c in Enum.GetValues(typeof(ColorMask))) {
                    string colorName = c.ToString();
                    if (!r.material.HasProperty(colorName))
                        continue;

                    originalColors.Add(new ColorSet() {
                        renderer = r,
                        property = c,
                        value = r.material.GetColor(colorName)
                    });
                    r.material.SetColor(colorName, replacecolor);
                }
            }

            return originalColors;
        }

        void RestoreColors(List<ColorSet> originalColors) {
            foreach (ColorSet s in originalColors) {
                Renderer r = s.renderer;
                if (r == null || r.material == null)
                    continue;
                r.material.SetColor(s.property.ToString(), s.value);
            }

            originalColors.Clear();
        }


        [Serializable]
        public class ModeHighlightList {
            [SerializeField]
            public List<ModeHighlightMapping> list = new List<ModeHighlightMapping>();

            public ModeHighlightMapping this[int i] {
                get { return list[i]; }
                set { list[i] = value; }
            }
        }

        [Serializable]
        public class ModeHighlightMapping {
            public int modeMask;
            public Color color;
        }

        [Flags]
        enum ColorMask {
            _Color,
            _EmissionColor,
            _SpecColor,
            _ReflectColor,
            _EmisColor
        }
        struct ColorSet {
            public Renderer renderer;
            public ColorMask property;
            public Color value;
        }

    }


    [CustomPropertyDrawer(typeof(VRIK_ModeHighlighter.ModeHighlightList))]
    public class VRIK_ModeHighlightListPropertyDrawer : PropertyDrawer {
        ReorderableList list;
        VRIK_ModeHighlighter.ModeHighlightList realproperty;
        string[] modeNames;
        Vector2 labelSize;
        bool dirty;
        int lastsize = 0;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Modes");
        static readonly GUIContent ColorLabel = new GUIContent("Color");

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                realproperty = (VRIK_ModeHighlighter.ModeHighlightList)fieldInfo.GetValue(property.serializedObject.targetObject);
                lastsize = realproperty.list.Count;
                list = new ReorderableList(realproperty.list, typeof(VRIK_ModeHighlighter.ModeHighlightMapping), true, true, true, true);
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
            dirty = false;
            realproperty = (VRIK_ModeHighlighter.ModeHighlightList)fieldInfo.GetValue(property.serializedObject.targetObject);

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            labelSize = GUI.skin.label.CalcSize(ColorLabel);

            getList(property);

            EditorGUI.BeginProperty(position, label, property);
            property.serializedObject.Update();
            //list.DoLayoutList();
            list.DoList(position);
            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();

            dirty |= (lastsize != realproperty.list.Count);
            lastsize = realproperty.list.Count;
            if (dirty) {
                EditorUtility.SetDirty(property.serializedObject.targetObject);
                EditorApplication.MarkSceneDirty();
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return getList(property).GetHeight();
        }

        void DrawMapping(Rect rect, int index, bool isActive, bool isFocused) {
            int newIntVal;
            Color newColorVal;
            rect.height = 16;
            VRIK_ModeHighlighter.ModeHighlightMapping mapping = realproperty[index];
            //var element = list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            Vector2 spacerSize = new Vector2(labelSize.x / 2, rect.height);
            Vector2 selectorSize = new Vector2((rect.width - (labelSize.x * 2 + spacerSize.x)) / 2, rect.height);
            Vector2 position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ModeSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.modeMask, modeNames);
            dirty |= (newIntVal != mapping.modeMask);
            mapping.modeMask = newIntVal;

            position.x += selectorSize.x + spacerSize.x;
            EditorGUI.LabelField(new Rect(position, labelSize), ColorLabel);
            position.x += labelSize.x;
            newColorVal = EditorGUI.ColorField(new Rect(position, selectorSize), mapping.color);
            dirty |= (newColorVal != mapping.color);
            mapping.color = newColorVal;
        }
    }//*/
}