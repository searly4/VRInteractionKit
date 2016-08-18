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
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Collections.ObjectModel;

namespace VRIK {

    public class VRInteractionAggregator : MonoBehaviour {
        public static VRInteractionAggregator instance;

        public PartialFixedArray InteractionTypes = new PartialFixedArray(32, 8, "Touch", "Use", "Grab");
        public PartialFixedArray Modes = new PartialFixedArray(32, 1, "Normal");
        public PartialFixedArray Actions = new PartialFixedArray(32, 1, "Touch");
        
        public static class Global {
            public static PartialFixedArray InteractionTypes { get { return instance.InteractionTypes; } }
            public static PartialFixedArray Modes { get { return instance.Modes; } }
            public static PartialFixedArray Actions { get { return instance.Actions; } }
        }

        /*
        internal static string[] _interactionTypes = new string[32];
        public static string[] InteractTypes { get { return _interactionTypes; } }

        public List<string> _customActions = new List<string>();
        public static string[] CusdtomActions {
            get { return (current != null) ? current._customActions.ToArray() : new string[0]; }
        }//*/
        
        List<VRInteractor> interactorList = new List<VRInteractor>();
        List<IActionSource> actionSourceList = new List<IActionSource>();
        List<VRInteractionManager> attachedManagers = new List<VRInteractionManager>();
        InteractablesList interactablesList = new InteractablesList();

        public ICollection<IActionSource> ActionSources {
            get { return actionSourceList.AsReadOnly(); }
        }

        public VRInteractionAggregator() {
            instance = this;
        }

        void Awake() {
            //Array.Copy(new string[]{ "Touch", "Use", "Grab"}, CustomInteractionTypes.Values, 3);
            //CustomInteractionTypes.fixedPortion = 8;
            //InteractionTypes.Init(32, 8, "Touch", "Use", "Grab");
            instance = this;
        }

        void OnEnable() {
            instance = this;
        }

        void OnDisable() {
            if (instance == this)
                instance = null;
        }

        void UpdateCache() {
            List<VRInteractionManager> managers = new List<VRInteractionManager>(transform.GetComponents<VRInteractionManager>());
            foreach (VRInteractionManager manager in managers) {
                if (attachedManagers.Contains(manager))
                    continue;
                attachedManagers.Add(manager);
            }
            foreach (VRInteractionManager manager in attachedManagers) {
                if (managers.Contains(manager))
                    continue;
                attachedManagers.Remove(manager);
            }
        }

        public event EventHandler<ValueEventArgs<IActionSource>> RegisteredActionSource;
        public event EventHandler<ValueEventArgs<IActionSource>> UnregisteredActionSource;
        public event EventHandler<ValueEventArgs<IActionSource>> ActionSourceStateChanged;

        public event EventHandler<ValueEventArgs<InteractablesList.InteractableData>> InteractableAdded;
        public event EventHandler<ValueEventArgs<InteractablesList.InteractableData>> InteractableRemoved;

        public void RegisterManager(VRInteractionManager source) {
            if (attachedManagers.Contains(source))
                return;

            attachedManagers.Add(source);
        }

        public void UnregisterManager(VRInteractionManager source) {
            if (attachedManagers.Contains(source))
                attachedManagers.Remove(source);
        }

        public void RegisterActionSource(IActionSource source) {
            if (actionSourceList.Contains(source))
                return;

            source.StateChanged += OnActionSourceStateChange;
            actionSourceList.Add(source);

            EventHandler<ValueEventArgs<IActionSource>> eh = RegisteredActionSource;
            if (eh != null)
                eh(this, new ValueEventArgs<IActionSource>(source));
        }

        public void UnregisterActionSource(IActionSource source) {
            if (actionSourceList.Contains(source))
                actionSourceList.Remove(source);
            source.StateChanged -= OnActionSourceStateChange;

            EventHandler<ValueEventArgs<IActionSource>> eh = UnregisteredActionSource;
            if (eh != null)
                eh(this, new ValueEventArgs<IActionSource>(source));
        }

        void OnActionSourceStateChange(IActionSource source, EState newState) {
            switch (newState) {
                case EState.Destroyed:
                    UnregisterActionSource(source);
                    break;
            }

            EventHandler<ValueEventArgs<IActionSource>> eh = ActionSourceStateChanged;
            if (eh != null)
                eh(this, new ValueEventArgs<IActionSource>(source));
        }

        public void RegisterInteractor(VRInteractor interactor) {
            if (!interactorList.Contains(interactor))
                interactorList.Add(interactor);
        }

        public void UnregisterInteractor(VRInteractor interactor) {
            if (interactorList.Contains(interactor))
                interactorList.Remove(interactor);
        }

        void OnInteractorStateChange(VRInteractor source, EState newState) {
            switch (newState) {
                case EState.Destroyed:
                    UnregisterInteractor(source);
                    break;
            }
        }
        
        public void NewInteractable(VRInteractor source, VRInteractor other, int[] availableInteractions) {
            InteractablesList.InteractableData item = interactablesList.Add(source, other, availableInteractions);
            if (item == null)
                return;

            EventHandler<ValueEventArgs<InteractablesList.InteractableData>> eh = InteractableAdded;
            if (eh != null)
                eh(this, new ValueEventArgs<InteractablesList.InteractableData>(item));
        }

        public void CancelInteractable(VRInteractor source, VRInteractor other) {
            InteractablesList.InteractableData item = interactablesList.Remove(source, other);
            if (item == null)
                return;

            EventHandler<ValueEventArgs<InteractablesList.InteractableData>> eh = InteractableRemoved;
            if (eh != null)
                eh(this, new ValueEventArgs<InteractablesList.InteractableData>(item));
        }

        public void TryInteract() {
            List<VRInteractionManager> managers = new List<VRInteractionManager>(transform.GetComponents<VRInteractionManager>());
        }

    }

    public class InteractablesList {
        List<InteractableData> interactablesList = new List<InteractableData>();

        public InteractableData Add(VRInteractor source, VRInteractor other, int[] availableInteractions) {
            if (FindInteractable(source, other) != null)
                return null;
            InteractableData item = new InteractableData(source, other, availableInteractions);
            this.interactablesList.Add(item);
            return item;
        }

        public InteractableData Remove(VRInteractor source, VRInteractor other) {
            InteractableData item = FindInteractable(source, other);
            if (item == null)
                return null;

            interactablesList.Remove(item);
            return item;
        }

        public InteractableData FindInteractable(VRInteractor a, VRInteractor b) {
            foreach (InteractableData item in interactablesList) {
                if ((item.a == a && item.b == b) || (item.a == b && item.b == a))
                    return item;
            }
            return null;
        }

        public InteractableData[] FindInteractables(VRInteractor a) {
            List<InteractableData> items = new List<InteractableData>();
            foreach (InteractableData item in interactablesList) {
                if (item.a == a || item.b == a)
                    items.Add(item);
            }
            return items.ToArray();
        }

        public InteractableData[] FindInteractables(VRInteractor[] interactors) {
            List<InteractableData> items = new List<InteractableData>();
            foreach (InteractableData item in interactablesList) {
                foreach (VRInteractor a in interactors)
                    if ((item.a == a || item.b == a) && !items.Contains(item))
                        items.Add(item);
            }
            return items.ToArray();
        }

        public class InteractableData {
            public VRInteractor a;
            public VRInteractor b;
            public int[] availableInteractions;
            public int[] activeInteractions = null;
            public InteractableData(VRInteractor a, VRInteractor b, int[] interactions) {
                this.a = a;
                this.b = b;
                this.availableInteractions = interactions;
            }
        }
    }



    [Serializable]
    public class PartialFixedArray {
        [SerializeField]
        protected string[] _values = new string[0];

        public string[] Values {
            get {
                if (_values == null || _values.Length == 0)
                    throw new Exception("Don't touch me.");
                return _values;
            }
        }//*/

        public string this[int index] {
            get { return _values[index]; }
            set {
                if (index < fixedPortion || _values[index] != value)
                    return;
                _values[index] = value;
                UpdateCache();
            }
        }

        [NonSerialized]
        public int fixedPortion = 8;

        string[] _abreviatedNames;
        int[] _abreviatedValues;
        int[] _abreviatedMaskValues;
        public string[] abreviatedNames {
            get {
                if (_abreviatedNames == null || _abreviatedNames.Length != _values.Length)
                    UpdateCache();
                return _abreviatedNames;
            }
        }
        public int[] abreviatedValues {
            get {
                if (_abreviatedValues == null || _abreviatedValues.Length != _values.Length)
                    UpdateCache();
                return _abreviatedValues;
            }
        }
        public int[] abreviatedMaskValues {
            get {
                if (_abreviatedMaskValues == null || _abreviatedMaskValues.Length != _values.Length)
                    UpdateCache();
                return _abreviatedMaskValues;
            }
        }

        public PartialFixedArray() { }

        public PartialFixedArray(int size, int fixedPortion, params string[] values) {
            this.fixedPortion = fixedPortion;
            _values = new string[size];
            Array.Copy(values, _values, values.Length);
        }

        public void Init(int size, int fixedPortion, params string[] values) {
            this.fixedPortion = fixedPortion;
            if (_values.Length != size)
                _values = new string[size];
            Array.Copy(values, _values, values.Length);
        }

        public void UpdateCache() {
            int count = 0;
            foreach (string s in _values)
                if (s != null && s != "")
                    count++;
            string[] names = new string[count];
            int[] vals = new int[count];
            int[] maskvals = new int[count];
            count = 0;
            for (int i = 0; i < _values.Length; i++) {
                string s = _values[i];
                if (s == null || s == "")
                    continue;
                names[count] = s;
                vals[count] = i;
                maskvals[count] = 1 << i;
                count++;
            }
            _abreviatedNames = names;
            _abreviatedValues = vals;
            _abreviatedMaskValues = maskvals;
        }
    }

    [CustomPropertyDrawer(typeof(PartialFixedArray))]
    public class PartialFixedFieldArrayDrawer : PropertyDrawer {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            float realheight = base.GetPropertyHeight(property, label);
            position.height = realheight;
            string labelprefix = "Element ";
            //int indent = EditorGUI.indentLevel;
            //EditorGUI.indentLevel++;
            EditorGUI.BeginProperty(position, label, property);
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label);
            EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            PartialFixedArray realproperty = (PartialFixedArray)fieldInfo.GetValue(property.serializedObject.targetObject);
            string[] values = realproperty.Values;
            if (property.isExpanded) {
                for (int i = 0; i < values.Length; i++) {
                    position.y += realheight;
                    string labeltext = string.Format("{0} {1}", labelprefix, i);
                    //SerializedProperty value = values.GetArrayElementAtIndex(i);
                    //EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
                    if (i < realproperty.fixedPortion)
                        EditorGUI.LabelField(position, labeltext, values[i]);
                    else
                        values[i] = EditorGUI.TextField(position, labeltext, values[i]);
                }//*/
            }
            EditorGUI.EndProperty();
            //EditorGUI.indentLevel = indent;
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            PartialFixedArray realproperty = (PartialFixedArray)fieldInfo.GetValue(property.serializedObject.targetObject);
            return base.GetPropertyHeight(property, label) * ((property.isExpanded) ? (realproperty.Values.Length + 1) : 1);
        }//*/
    }//*/

    public abstract class ListSource {
        public abstract string[] Ref { get; }
    }

    public class InteractionTypesSource : ListSource {
        public override string[] Ref { get { return VRInteractionAggregator.Global.InteractionTypes.Values; } }
    }
    public class ModesSource : ListSource {
        public override string[] Ref { get { return VRInteractionAggregator.Global.Modes.Values; } }
    }
    public class ActionsSource : ListSource {
        public override string[] Ref { get { return VRInteractionAggregator.Global.Actions.Values; } }
    }

    [Serializable]
    public class ListSelector {
        public static string[] GetValues(Type staticDataSource, string dataSourcePropertyName) {
            PropertyInfo pi = staticDataSource.GetProperty(dataSourcePropertyName, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            return (string[])pi.GetValue(null, null);
        }

        [SerializeField]
        internal int _value;

        public int Value { get { return _value; } }

        public virtual string[] Values {
            get { // need something here because property drawers don't recognize abstract classes
                return new string[] { };
            }
        }

        [NonSerialized]
        PropertyInfo propertyInfoCache;

        public ListSelector() { }
    }

    [Serializable]
    public class ListSelector<T> : ListSelector where T : ListSource, new() {
        ListSource listSource = new T();
        public override string[] Values {
            get {
                return listSource.Ref;
            }
        }
    }

    [CustomPropertyDrawer(typeof(ListSelector))]
    public class ListSelectorDrawer : PropertyDrawer {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            ListSelector realproperty = (ListSelector)fieldInfo.GetValue(property.serializedObject.targetObject);
            string[] sourceList = realproperty.Values;
            List<string> nameVals = new List<string>();
            List<int> intVals = new List<int>();
            for (int i = 0; i < sourceList.Length; i++) {
                string value = sourceList[i];
                if (value != null && value != "") {
                    nameVals.Add(value);
                    intVals.Add(i);
                }
            }
            realproperty._value = (int)EditorGUI.IntPopup(position, label.text, realproperty._value, nameVals.ToArray(), intVals.ToArray());
            EditorGUI.EndProperty();
        }
    }


    [Serializable]
    public class ActionTypes {
        public int value;
    }

    [CustomPropertyDrawer(typeof(ActionTypes))]
    public class ActionTypesDrawer : PropertyDrawer {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty action = property.FindPropertyRelative("value");
            //string[] actionNames = VRInteractionManager.CusdtomActions;
            //int[] actionVals = new int[actionNames.Length];
            //for (int i = 0; i < actionVals.Length; i++)
                //actionVals[i] = i;
            //action.intValue = (int)EditorGUI.IntPopup(position, label.text, action.intValue, actionNames, actionVals);
            EditorGUI.EndProperty();
        }
    }//*/
    
    public class ExtensibleFlags {
        public struct Value {
            int index;
            ExtensibleFlags parent;
        }

        public List<string> Values;

        public ExtensibleFlags(params string[] values) {
            this.Values = new List<string>(values);
        }
    }


    /*
    [Serializable]
    public struct InteractionMapping {
        public InteractionTypes interactionType;
        public int action;
        public int mode;
        public bool toggle;
    }

    [CustomPropertyDrawer(typeof(InteractionMapping))]
    public class InteractionMappingDrawer : PropertyDrawer {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Don't make child fields be indented
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Calculate rects
            Rect amountRect = new Rect(position.x, position.y, position.width, position.height);

            InteractionMapping mapping = (InteractionMapping)(object)property.serializedObject;
            SerializedProperty interactionType = property.FindPropertyRelative("interactionType");
            SerializedProperty action = property.FindPropertyRelative("action");
            SerializedProperty mode = property.FindPropertyRelative("mode");
            SerializedProperty toggle = property.FindPropertyRelative("toggle");

            // Draw fields - passs GUIContent.none to each so they are drawn without labels
            //EditorGUI.MaskField(amountRect, "Interaction Type Mask", property.FindPropertyRelative("value").intValue, InteractionMask.typeNames.ToArray());
            mapping.interactionType = (InteractionTypes)EditorGUI.EnumPopup(amountRect, mapping.interactionType);

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }

    [Serializable]
    public struct InteractionMask {
        public int value;

        public static int GetMask(params string[] typeNames) {
            int mask = 0;
            List<string> typeList = new List<string>(VRInteractionManager.InteractTypes);
            foreach (string s in typeNames) {
                if (!typeList.Contains(s))
                    continue;
                mask |= 1 << typeList.IndexOf(s);
            }
            return mask;
        }

        public static string LayerToName(int layer) {
            if (layer < 0 || layer > 31)
                throw new Exception(string.Format("Invalid layer number: {0}", layer));

            return VRInteractionManager.InteractTypes[layer];
        }

        public static int NameToLayer(string typeName) {
            List<string> typeList = new List<string>(VRInteractionManager.InteractTypes);
            if (typeList.Contains(typeName))
                return typeList.IndexOf(typeName);
            return -1;
        }

        public static implicit operator int(InteractionMask mask) { return mask.value; }
        public static implicit operator InteractionMask(int intVal) { return new InteractionMask { value = intVal }; }
        //public static T GetValue<T>(SerializedProperty property) { return }
    }

    [CustomPropertyDrawer(typeof(InteractionMask))]
    public class InteractionMaskDrawer : PropertyDrawer {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Don't make child fields be indented
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Calculate rects
            Rect amountRect = new Rect(position.x, position.y, position.width, position.height);

            SerializedProperty flags = property.FindPropertyRelative("value");

            // Draw fields - passs GUIContent.none to each so they are drawn without labels
            //EditorGUI.MaskField(amountRect, "Interaction Type Mask", property.FindPropertyRelative("value").intValue, InteractionMask.typeNames.ToArray());
            flags.intValue = EditorGUI.MaskField(amountRect, flags.intValue, VRInteractionManager.InteractTypes);

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }

    public class EventArgs<T> : EventArgs {
        public T value;
        public EventArgs(T value) {
            this.value = value;
        }
    }//*/
}
