/*
Copyright (c) 2016 Scott Early

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEditorInternal;

namespace VRIK {
    public enum InteractionTypes {
        Touch, Use, Grab
    }

    public enum EAction { None, Oneshot, Toggle, Start, Stop }
    public enum EActionEdge { Momentary, ToggleLeading, ToggleTrailing, OneshotLeading, OneshotTrailing }

    public enum EState { Started, Enabled, Disabled, Destroyed }

    public delegate void ActionEventHandler(ActionTracker tracker);
    public delegate void StateChangeEventHandler(IActionSource source, EState newState);
    public delegate void ModeChangeEventHandler(object source, int oldMode, int newMode);
    public delegate void InteractionEventHandler(InteractionTracker tracker);
    public delegate void InteractionInstanceEventHandler(InteractionInstanceTracker tracker);
    public delegate void InteractionLinkEventHandler(VRInteractor source, InteractionInstanceTracker tracker);
    public delegate void ActionSourceInteractionEventHandler(IActionSource source, InteractionInstanceTracker tracker);

    public static class Utilities {
        public static Dictionary<int, string> StripList(string[] list) {
            Dictionary<int, string> map = new Dictionary<int, string>();
            for (int i = 0; i < list.Length; i++) {
                string value = list[i];
                if (value != null && value != "") {
                    map[i] = value;
                }
            }
            return map;
        }
    }

    public class ActionEventArgs : EventArgs {
        public IActionSource source { get; protected set; }
        public int index { get; protected set; }
        public bool wasReceived { get; protected set; }

        public ActionEventArgs(IActionSource source, int index) {
            this.index = index;
            this.source = source;
            wasReceived = false;
        }

        public void Received() {
            wasReceived = true;
        }
    }

    public class ToggleActionEventArgs : ActionEventArgs {
        public bool state { get; protected set; }
        public ToggleActionEventArgs(IActionSource source, int index, bool state) : base(source, index) {
            this.state = state;
        }
    }

    public class TouchActionEventArgs : ToggleActionEventArgs {
        public TouchActionEventArgs(IActionSource source, int index, bool state) : base(source, index, state) {
        }
    }

    public class InteractionNotifier {
        public int Index;
        public delegate void InteractionEventHandler(InteractionNotifier source);
        public event InteractionEventHandler Released;

        public InteractionNotifier(int index) {
            this.Index = index;
        }

        public void Release() {
            InteractionEventHandler eh = Released;
            if (eh != null)
                eh(this);
        }
    }

    public class ValueEventArgs<T> : EventArgs {
        public T Value { get; private set; }
        public ValueEventArgs(T value) {
            this.Value = value;
        }
    }

    public abstract class ActionTracker {
        public event ActionEventHandler Released;

        public IActionSource source { get; private set; }
        public int actionIndex { get; private set; }
        public EActionEdge actionEdge { get; private set; }

        public ActionTracker(IActionSource source, int actionIndex, EActionEdge actionEdge) {
            this.source = source;
            this.actionIndex = actionIndex;
            this.actionEdge = actionEdge;
        }

        public virtual void Release() {
            ActionEventHandler eh = Released;
            if (eh != null)
                eh(this);
            Released = null; // dump delegates so GC can clean up their objects
        }
    }

    public class ActionTracker<T> : ActionTracker {
        public delegate void ActionChangedEventHandler(ActionTracker tracker, T value);
        public event ActionChangedEventHandler ValueChanged;

        public ActionTracker(IActionSource source, int actionIndex, EActionEdge actionEdge, T initialValue) : base(source, actionIndex, actionEdge) {
            _value = initialValue;
        }

        T _value;
        public T value {
            get { return _value; }
            set {
                _value = value;
                OnChanged(value);
            }
        }

        public void OnChanged(T value) {
            ActionChangedEventHandler eh = ValueChanged;
            if (eh != null)
                eh(this, value);
        }

        public override void Release() {
            base.Release();
            ValueChanged = null;
        }
    }

    public class OneShotActionTracker : ActionTracker {
        public event ActionEventHandler Fired;

        public OneShotActionTracker(IActionSource source, int actionIndex, EActionEdge actionEdge) : base(source, actionIndex, actionEdge) { }

        public void Fire() {
            ActionEventHandler eh = Fired;
            if (eh != null)
                eh(this);
        }

        public override void Release() {
            base.Release();
            Fired = null; // dump delegates so GC can clean up their objects
        }
    }

    #region InteractionTracker classes
    public abstract class InteractionTracker {
        public event InteractionEventHandler Released;
        public event InteractionInstanceEventHandler Accepted;

        public int interactionIndex { get; private set; }
        public bool isHeld { get; private set; }

        public InteractionTracker(int interactionIndex) {
            this.interactionIndex = interactionIndex;
        }

        public void Accept(InteractionInstanceTracker instanceTracker) {
            isHeld = true;
            InteractionInstanceEventHandler eh = Accepted;
            if (eh != null)
                eh(instanceTracker);
        }

        public virtual void Release() {
            isHeld = false;
            InteractionEventHandler eh = Released;
            if (eh != null)
                eh(this);
            Released = null; // dump delegates so GC can clean up their objects
        }
    }

    public class InteractionTracker<T> : InteractionTracker where T : IEquatable<T> {
        public delegate void InteractionChangedEventHandler(InteractionTracker tracker, T value);
        public event InteractionChangedEventHandler ValueChanged;

        public InteractionTracker(int interactionIndex, T initialValue, T releaseValue, bool changeValueOnRelease) : base(interactionIndex) {
            _value = initialValue;
            this.ReleaseValue = releaseValue;
            this.ChangeValueOnRelease = changeValueOnRelease;
        }

        public bool ChangeValueOnRelease;
        public T ReleaseValue;

        T _value;
        public T value {
            get { return _value; }
            set {
                if (_value.Equals(value))
                    return;
                _value = value;
                OnChanged(value);
            }
        }

        public void OnChanged(T value) {
            InteractionChangedEventHandler eh = ValueChanged;
            if (eh != null)
                eh(this, value);
        }

        public override void Release() {
            if (ChangeValueOnRelease)
                value = ReleaseValue;
            base.Release();
            ValueChanged = null;
        }
    }

    public class OneShotInteractionTracker : InteractionTracker {
        public event InteractionEventHandler Fired;

        public OneShotInteractionTracker(int interactionIndex) : base(interactionIndex) { }

        public void Fire() {
            InteractionEventHandler eh = Fired;
            if (eh != null)
                eh(this);
        }

        public override void Release() {
            base.Release();
            Fired = null; // dump delegates so GC can clean up their objects
        }
    }
    #endregion

    public class InteractionInstanceTracker {
        public event InteractionInstanceEventHandler Released;

        public InteractionTracker itracker { get; private set; }
        public VRInteractor interactor { get; private set; }
        public VRInteraction handler { get; private set; }
        public VRInteractor otherInteractor { get; private set; }

        public InteractionInstanceTracker(InteractionTracker itracker, VRInteractor interactor, VRInteraction handler, VRInteractor otherInteractor) {
            this.itracker = itracker;
            this.interactor = interactor;
            this.handler = handler;
            this.otherInteractor = otherInteractor;
            itracker.Released += OnITrackerReleased;
        }

        private void OnITrackerReleased(InteractionTracker tracker) {
            itracker.Released -= OnITrackerReleased;
            Release();
        }

        public virtual void Release() {
            InteractionInstanceEventHandler eh = Released;
            if (eh != null)
                eh(this);
            Released = null; // dump delegates so GC can clean up their objects
        }
    }

    public class TouchTracker {
        public delegate void TouchTrackerEventHandler(VRInteractor source, TouchTracker tracker);
        public event TouchTrackerEventHandler Released;

        public VRInteractor A { get; private set; }
        public VRInteractor B { get; private set; }

        public TouchTracker(VRInteractor a, VRInteractor b) {
            this.A = a;
            this.B = b;
        }

        public void Release(VRInteractor source) {
            TouchTrackerEventHandler eh = Released;
            if (eh != null)
                eh(source, this);
            Released = null; // dump delegates so GC can clean up their objects
        }

        public bool Contains(VRInteractor interactor) {
            return A == interactor || B == interactor;
        }

        public VRInteractor GetOther(VRInteractor interactor) {
            if (A == interactor)
                return B;
            if (B == interactor)
                return A;
            return null;
        }
    }

    public delegate void AttachedInteractorEventHandler(IActionSource source, VRInteractor interactor);

    public interface IActionSource {
        VRInteractor[] LocalInteractors { get; }
        int Mode { get; set; }
        bool IsEnabled { get; }
        event StateChangeEventHandler StateChanged;
        event AttachedInteractorEventHandler InteractorAdded;
        event AttachedInteractorEventHandler InteractorRemoved;
        event ActionSourceInteractionEventHandler InteractionStarted;
        event ActionEventHandler ActionEvent;
        void AddInteractor(VRInteractor interactor);
        void RemoveInteractor(VRInteractor interactor);
        GameObject gameObject { get; }
    }

    #region ButtonMappingList
    [Serializable]
    public class ButtonMappingList {
        [SerializeField]
        public List<ButtonMapping> list = new List<ButtonMapping>();

        [NonSerialized]
        public IButtonSource buttonSource;

        public ButtonMapping this[int i] {
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
    public class ButtonMapping {
        public int modeMask;
        public int button;
        public int action;
        public EActionEdge toggle;
    }

    [CustomPropertyDrawer(typeof(ButtonMappingList))]
    public class ButtonMappingListDrawer : PropertyDrawer {
        ReorderableList list;
        ButtonMappingList realproperty;
        string[] handlerNames;
        string[] modeNames;
        string[] buttonNames;
        int[] buttonVals;
        string[] actionNames;
        int[] actionVals;
        Vector2 labelSize;
        bool dirty;
        int lastsize = 0;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Modes");
        static readonly GUIContent ButtonSelectLabel = new GUIContent("Button");
        static readonly GUIContent ActionSelectLabel = new GUIContent("Action");
        static readonly GUIContent ActivationSelectLabel = new GUIContent("Activation");

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                realproperty = (ButtonMappingList)fieldInfo.GetValue(property.serializedObject.targetObject);
                lastsize = realproperty.list.Count;
                list = new ReorderableList(realproperty.list, typeof(ButtonMapping), true, true, true, true);
                //list = new ReorderableList(property.serializedObject, property, true, true, true, true);

                list.drawElementCallback = DrawMapping;
                list.elementHeight = 16 * 2 + 4;
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
            realproperty = (ButtonMappingList)fieldInfo.GetValue(property.serializedObject.targetObject);

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            buttonNames = realproperty.buttonSource.names;
            buttonVals = realproperty.buttonSource.values;

            this.actionNames = VRInteractionAggregator.Global.Actions.abreviatedNames;
            this.actionVals = VRInteractionAggregator.Global.Actions.abreviatedValues;

            labelSize = GUI.skin.label.CalcSize(ActivationSelectLabel);

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
            EActionEdge newActionEdgeVal;
            rect.height = 16;
            ButtonMapping mapping = realproperty[index];
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
            EditorGUI.LabelField(new Rect(position, labelSize), ButtonSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.button, buttonNames, buttonVals);
            dirty |= (newIntVal != mapping.button);
            mapping.button = newIntVal;

            rect.y += rect.height;
            position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ActivationSelectLabel);
            position.x += labelSize.x;
            newActionEdgeVal = (EActionEdge)EditorGUI.EnumPopup(new Rect(position, selectorSize), mapping.toggle);
            dirty |= (newActionEdgeVal != mapping.toggle);
            mapping.toggle = newActionEdgeVal;

            position.x += selectorSize.x + spacerSize.x;
            EditorGUI.LabelField(new Rect(position, labelSize), ActionSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.action, actionNames, actionVals);
            dirty |= (newIntVal != mapping.action);
            mapping.action = newIntVal;
        }
    }//*/
    #endregion

    #region InteractionFilterList
    [Serializable]
    public class InteractionFilterList {
        [SerializeField]
        public List<InteractionFilter> list = new List<InteractionFilter>();

        public InteractionFilter this[int i] {
            get { return list[i]; }
            set { list[i] = value; }
        }
    }

    [Serializable]
    public class InteractionFilter {
        public int modeMask;
        public int interactionIndex;
    }

    [CustomPropertyDrawer(typeof(InteractionFilterList))]
    public class InteractionFilterListDrawer : PropertyDrawer {
        ReorderableList list;
        InteractionFilterList realproperty;
        string[] modeNames;
        string[] interactionNames;
        int[] interactionVals;
        Vector2 labelSize;
        bool dirty;
        int lastsize = 0;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Modes");
        static readonly GUIContent InteractionSelectLabel = new GUIContent("Interaction");

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                realproperty = (InteractionFilterList)fieldInfo.GetValue(property.serializedObject.targetObject);
                lastsize = realproperty.list.Count;
                list = new ReorderableList(realproperty.list, typeof(InteractionFilter), true, true, true, true);
                //list = new ReorderableList(property.serializedObject, property, true, true, true, true);

                list.drawElementCallback = DrawMapping;
                list.elementHeight = 16 * 1 + 4;
                //list.headerHeight = 32;
                list.draggable = false;

                list.drawHeaderCallback = (Rect rect) => {
                    EditorGUI.LabelField(rect, "Filters");
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
            realproperty = (InteractionFilterList)fieldInfo.GetValue(property.serializedObject.targetObject);

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            this.interactionNames = VRInteractionAggregator.Global.InteractionTypes.abreviatedNames;
            this.interactionVals = VRInteractionAggregator.Global.InteractionTypes.abreviatedValues;

            labelSize = GUI.skin.label.CalcSize(InteractionSelectLabel);

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
            rect.height = 16;
            InteractionFilter mapping = realproperty[index];
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
            EditorGUI.LabelField(new Rect(position, labelSize), InteractionSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.interactionIndex, interactionNames, interactionVals);
            dirty |= (newIntVal != mapping.interactionIndex);
            mapping.interactionIndex = newIntVal;
        }
    }//*/
    #endregion

    #region InteractionFilter2List
    [Serializable]
    public class InteractionFilter2List {
        [SerializeField]
        public List<InteractionFilter2> list = new List<InteractionFilter2>();

        public InteractionFilter2 this[int i] {
            get { return list[i]; }
            set { list[i] = value; }
        }
    }

    [Serializable]
    public class InteractionFilter2 {
        public int modeMask;
        public int modeMask2;
        public int interactionIndex;
    }

    [CustomPropertyDrawer(typeof(InteractionFilter2List))]
    public class InteractionFilter2ListDrawer : PropertyDrawer {
        ReorderableList list;
        InteractionFilter2List realproperty;
        string[] modeNames;
        string[] interactionNames;
        int[] interactionVals;
        Vector2 labelSize;
        bool dirty;
        int lastsize = 0;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Local Modes");
        static readonly GUIContent Mode2SelectLabel = new GUIContent("Remote Modes");
        static readonly GUIContent InteractionSelectLabel = new GUIContent("Interaction");

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                realproperty = (InteractionFilter2List)fieldInfo.GetValue(property.serializedObject.targetObject);
                lastsize = realproperty.list.Count;
                list = new ReorderableList(realproperty.list, typeof(InteractionFilter2), true, true, true, true);
                //list = new ReorderableList(property.serializedObject, property, true, true, true, true);

                list.drawElementCallback = DrawMapping;
                list.elementHeight = 16 * 2 + 4;
                //list.headerHeight = 32;
                list.draggable = false;

                list.drawHeaderCallback = (Rect rect) => {
                    EditorGUI.LabelField(rect, "Filters");
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
            realproperty = (InteractionFilter2List)fieldInfo.GetValue(property.serializedObject.targetObject);

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            this.interactionNames = VRInteractionAggregator.Global.InteractionTypes.abreviatedNames;
            this.interactionVals = VRInteractionAggregator.Global.InteractionTypes.abreviatedValues;

            labelSize = GUI.skin.label.CalcSize(Mode2SelectLabel);

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
            rect.height = 16;
            InteractionFilter2 mapping = realproperty[index];
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
            EditorGUI.LabelField(new Rect(position, labelSize), ModeSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.modeMask2, modeNames);
            dirty |= (newIntVal != mapping.modeMask2);
            mapping.modeMask2 = newIntVal;

            rect.y += rect.height;
            position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), InteractionSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.interactionIndex, interactionNames, interactionVals);
            dirty |= (newIntVal != mapping.interactionIndex);
            mapping.interactionIndex = newIntVal;
        }
    }//*/
    #endregion

    #region ModeFilter2List
    [Serializable]
    public class ModeFilter2List {
        [SerializeField]
        public List<ModeFilter2> list = new List<ModeFilter2>();

        public ModeFilter2 this[int i] {
            get { return list[i]; }
            set { list[i] = value; }
        }
    }

    [Serializable]
    public class ModeFilter2 {
        public int modeMask;
        public int modeMask2;
    }

    [CustomPropertyDrawer(typeof(ModeFilter2List))]
    public class ModeFilter2ListDrawer : PropertyDrawer {
        ReorderableList list;
        ModeFilter2List realproperty;
        string[] modeNames;
        string[] interactionNames;
        int[] interactionVals;
        Vector2 labelSize;
        bool dirty;
        int lastsize = 0;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Local Modes");
        static readonly GUIContent Mode2SelectLabel = new GUIContent("Remote Modes");

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                realproperty = (ModeFilter2List)fieldInfo.GetValue(property.serializedObject.targetObject);
                lastsize = realproperty.list.Count;
                list = new ReorderableList(realproperty.list, typeof(ModeFilter2), true, true, true, true);
                //list = new ReorderableList(property.serializedObject, property, true, true, true, true);

                list.drawElementCallback = DrawMapping;
                list.elementHeight = 16 * 1 + 4;
                //list.headerHeight = 32;
                list.draggable = false;

                list.drawHeaderCallback = (Rect rect) => {
                    EditorGUI.LabelField(rect, "Filters");
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
            realproperty = (ModeFilter2List)fieldInfo.GetValue(property.serializedObject.targetObject);

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            labelSize = GUI.skin.label.CalcSize(Mode2SelectLabel);

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
            rect.height = 16;
            ModeFilter2 filter = realproperty[index];
            //var element = list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            Vector2 spacerSize = new Vector2(labelSize.x / 2, rect.height);
            Vector2 selectorSize = new Vector2((rect.width - (labelSize.x * 2 + spacerSize.x)) / 2, rect.height);
            Vector2 position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ModeSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.MaskField(new Rect(position, selectorSize), filter.modeMask, modeNames);
            dirty |= (newIntVal != filter.modeMask);
            filter.modeMask = newIntVal;

            position.x += selectorSize.x + spacerSize.x;
            EditorGUI.LabelField(new Rect(position, labelSize), Mode2SelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.MaskField(new Rect(position, selectorSize), filter.modeMask2, modeNames);
            dirty |= (newIntVal != filter.modeMask2);
            filter.modeMask2 = newIntVal;
        }
    }//*/
    #endregion

}
