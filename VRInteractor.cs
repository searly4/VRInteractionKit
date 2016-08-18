using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Events;
using UnityEditor;
using UnityEditorInternal;

namespace VRIK {
    public class VRInteractor : MonoBehaviour {

        [SerializeField]
        public VRInteractor inheritModeFrom;

        public int mode;

        //public List<InteractionMapping> interactionMappings = new List<InteractionMapping>();
        [SerializeField]
        public InteractionMappingList interactionMappings = new InteractionMappingList();

        public event TouchTracker.TouchTrackerEventHandler TouchStarted;
        public event InteractionLinkEventHandler InteractionStarted;
        public event InteractionLinkEventHandler InteractionStopped;
        public event ModeChangeEventHandler ModeChanged;

        protected List<VRInteraction> interactionHandlers = new List<VRInteraction>();
        protected Dictionary<VRInteractor, int> touchCounts = new Dictionary<VRInteractor, int>();
        protected Dictionary<int, VRInteractor[]> contextInteractions = new Dictionary<int, VRInteractor[]>();
        //protected Dictionary<VRInteractor, int[]> currentInteractors = new Dictionary<VRInteractor, int[]>();
        //protected SortedDictionary<int, InteractionNotifier> notifiers = new SortedDictionary<int, InteractionNotifier>();
        protected Dictionary<VRInteractor, TouchTracker> touchList = new Dictionary<VRInteractor, TouchTracker>();
        protected List<InteractionInstanceTracker> interactionTrackers = new List<InteractionInstanceTracker>();

        public VRInteractor parent { get; protected set; }

        public VRInteractor[] touchedObjects {
            get {
                VRInteractor[] retval = new VRInteractor[touchCounts.Count];
                touchCounts.Keys.CopyTo(retval, 0);
                return retval;
            }
        }

        public InteractionInstanceTracker[] interactions {
            get {
                return interactionTrackers.ToArray();
            }
        }

        public int Mode {
            get { return mode; }
            set {
                if (value == mode)
                    return;

                int oldMode = mode;
                mode = value;

                UpdateInteractions();

                ModeChangeEventHandler eh = ModeChanged;
                if (eh != null)
                    eh(this, oldMode, value);
            }
        }


        protected void OnEnable() {
            if (inheritModeFrom != null)
                inheritModeFrom.ModeChanged += OnParentModeChanged;
            UpdateCache();

            //VRInteractionManager.instance.RegisterInteractor(this);
        }

        protected void OnDisable() {
            parent = null;
            //if (VRInteractionManager.instance != null)
            //VRInteractionManager.instance.UnregisterInteractor(this);
            if (inheritModeFrom != null)
                inheritModeFrom.ModeChanged -= OnParentModeChanged;
        }

        public void RegisterHandler(VRInteraction handler) {
            interactionHandlers.Add(handler);
        }

        public void UnregisterHandler(VRInteraction handler) {
            interactionHandlers.Remove(handler);
        }
        
        void UpdateCache() {
            if (parent == null && transform.parent != null)
                parent = transform.parent.GetComponentInParent<VRInteractor>();

            foreach (InteractionMapping mapping in interactionMappings.list) {
                if (mapping.InteractionHandlerInstance != null)
                    continue;
                if (string.IsNullOrEmpty(mapping.InteractionHandler) || mapping.InteractionHandler == "None")
                    continue;
                mapping.InteractionHandlerInstance = (VRInteraction)GetComponent(mapping.InteractionHandler);
            }
        }

        void UpdateInteractions() {
            Dictionary<int, List<VRInteractor>> foundInteractions = new Dictionary<int, List<VRInteractor>>();
            // search for compatible mappings with each touched interactor
            foreach (InteractionMapping mapping in interactionMappings.list) {
                if (mapping.Interaction == 0)
                    continue; // exclude touches
                if ((mapping.ModeMask & (1 << mode)) == 0)
                    continue; // mapping does not match interactor's current mode
                List<VRInteractor> matchedInteractors;
                if (!foundInteractions.TryGetValue(mapping.Interaction, out matchedInteractors))
                    matchedInteractors = new List<VRInteractor>();

                foreach (VRInteractor other in touchCounts.Keys) {
                    bool caninteract = false;
                    foreach (InteractionMapping othermapping in other.interactionMappings.list) {
                        if (mapping.Interaction != othermapping.Interaction)
                            continue;
                        if ((mapping.ModeMask & (1 << mode)) == 0 || (mapping.ModeMask2 & (1 << other.mode)) == 0)
                            continue; // mapping does not match interactor's current mode
                        if ((othermapping.ModeMask2 & (1 << mode)) == 0 || (othermapping.ModeMask & (1 << other.mode)) == 0)
                            continue; // mapping does not match interactor's current mode
                        if ((mapping.InteractionHandlerInstance != null && mapping.InteractionHandlerInstance.CanInteract(other)) ||
                            (othermapping.InteractionHandlerInstance != null && othermapping.InteractionHandlerInstance.CanInteract(this))
                            ) {
                            // found an interaction handler that will accept this interactor pairing
                            caninteract = true;
                            break;
                        }
                    }
                    if (caninteract) {
                        matchedInteractors.Add(other);
                        break;
                    }
                }
                if (matchedInteractors.Count > 0)
                    foundInteractions.Add(mapping.Interaction, matchedInteractors);
            }

            contextInteractions.Clear();
            foreach (var t in foundInteractions) {
                contextInteractions[t.Key] = t.Value.ToArray();
            }
        }

        public bool HasInteraction(int interactionIndex) {
            return contextInteractions.ContainsKey(interactionIndex);
        }

        public int HasInteraction(Type interactionType) {
            // check for compatible mappings

            int modeMask = 0;
            List<int> interactions = new List<int>();
            foreach (InteractionMapping mapping1 in interactionMappings.list) {
                if (mapping1.InteractionHandlerInstance == null || !interactionType.IsAssignableFrom(mapping1.InteractionHandlerInstance.GetType()))
                    continue;
                modeMask |= mapping1.ModeMask;
            }

            return modeMask;
        }

        public int GetInteractionModes(int interactionIndex) {
            // check for compatible mappings

            int modeMask = 0;
            List<int> interactions = new List<int>();
            foreach (InteractionMapping mapping1 in interactionMappings.list) {
                if (mapping1.Interaction != interactionIndex)
                    continue;
                modeMask |= mapping1.ModeMask;
            }

            return modeMask;
        }
        
        public VRInteractor[] GetTouching() {
            var t = touchList.Keys;
            VRInteractor[] retval = new VRInteractor[t.Count];
            t.CopyTo(retval, 0);
            return retval;
        }

        public void DispatchAll(InteractionTracker tracker) {
            List<VRInteractor> list = new List<VRInteractor>(touchList.Keys); // dispatches sometimes alter the list
            foreach (VRInteractor other in list) {
                Dispatch(tracker, other);
                other.Dispatch(tracker, this);
            }
        }

        public void DispatchBinary(InteractionTracker tracker, VRInteractor other) {
            Dispatch(tracker, other);
            other.Dispatch(tracker, this);
        }

        public virtual void Dispatch(InteractionTracker tracker, VRInteractor other) {
            InteractionMapping[] list = interactionMappings.list.ToArray();
            foreach (InteractionMapping mapping1 in list) {
                if (tracker.isHeld)
                    break;

                if (mapping1.InteractionHandlerInstance == null ||
                    mapping1.Interaction != tracker.interactionIndex ||
                    (mapping1.ModeMask & (1 << mode)) == 0 ||
                    (mapping1.ModeMask2 & (1 << other.mode)) == 0
                    )
                    continue;

                InteractionInstanceTracker t = mapping1.InteractionHandlerInstance.TryInteract(tracker, other);
                if (t != null) {
                    interactionTrackers.Add(t);
                    other.interactionTrackers.Add(t);
                    t.Released += OnInteractionStopped;
                    t.Released += other.OnInteractionStopped;
                    OnInteractionStarted(other, t, mapping1.InteractionHandlerInstance);
                    other.OnInteractionStarted(this, t, mapping1.InteractionHandlerInstance);
                }
            }
        }

        private void OnParentModeChanged(object source, int oldMode, int newMode) {
            Mode = newMode;
        }

        private void OnInteractionStarted(VRInteractor other, InteractionInstanceTracker tracker, VRInteraction handler) {
            InteractionLinkEventHandler eh = InteractionStarted;
            if (eh != null)
                eh(this, tracker);
        }

        private void OnInteractionStopped(InteractionInstanceTracker tracker) {
            tracker.Released -= OnInteractionStopped;
            interactionTrackers.Remove(tracker);
            InteractionLinkEventHandler eh = InteractionStopped;
            if (eh != null)
                eh(this, tracker);
        }

        public void StartedTouching(VRInteractor other) {
            if (touchCounts.ContainsKey(other)) {
                touchCounts[other]++;
                return;
            }
            touchCounts[other] = 1;


            TouchTracker tracker = null;
            other.touchList.TryGetValue(this, out tracker);
            if (tracker == null)
                tracker = new TouchTracker(this, other);
            touchList[other] = tracker;

            UpdateInteractions();

            other.ModeChanged += OnTouchedInteractorModeChange;

            TouchTracker.TouchTrackerEventHandler eh = TouchStarted;
            if (eh != null)
                eh(this, tracker);
        }

        private void OnTouchedInteractorModeChange(object source, int oldMode, int newMode) {
            UpdateInteractions();
        }

        public void StoppedTouching(VRInteractor other) {
            if (!touchCounts.ContainsKey(other)) {
                Debug.LogWarning("Excessive StoppedTouching calls: " + this.ToString() + ", " + other.ToString());
                return;
            }
            if (touchCounts[other] > 1) {
                touchCounts[other]--;
                return;
            }
            touchCounts.Remove(other);
            other.ModeChanged -= OnTouchedInteractorModeChange;
            UpdateInteractions();

            //VRInteractionAggregator.instance.CancelInteractable(this, other);
            TouchTracker tracker = null;
            if (!touchList.TryGetValue(other, out tracker) && !other.touchList.TryGetValue(this, out tracker))
                return;
            if (touchList.ContainsKey(other))
                touchList.Remove(other);
            if (other.touchList.ContainsKey(this))
                other.touchList.Remove(this);

            tracker.Release(this);
        }

        /*void OnTriggerEnter(Collider collider) {
            if (TouchStarted == null || collider == null)
                return; // don't need to handle this

            VRInteractor other = collider.GetComponent<VRInteractor>();
            if (other == null)
                return;

            TouchTracker tracker = null;
            other.touchList.TryGetValue(this, out tracker);
            if (tracker == null)
                tracker = new TouchTracker(this, other);
            touchList[other] = tracker;
            TouchTracker.TouchTrackerEventHandler eh = TouchStarted;
            if (eh != null)
                eh(this, tracker);
        }

        void OnTriggerExit(Collider collider) {
            if (touchList.Count == 0 || collider == null)
                return;

            VRInteractor other = collider.GetComponent<VRInteractor>();
            if (other == null)
                return;

            //VRInteractionAggregator.instance.CancelInteractable(this, other);
            TouchTracker tracker = null;
            if (!touchList.TryGetValue(other, out tracker) && !other.touchList.TryGetValue(this, out tracker))
                return;
            tracker.Release(this);
            if (touchList.ContainsKey(other))
                touchList.Remove(other);
            if (other.touchList.ContainsKey(this))
                other.touchList.Remove(this);
        }//*/

        [Serializable]
        public class InteractionMappingList {
            [SerializeField]
            public List<InteractionMapping> list = new List<InteractionMapping>();

            public InteractionMapping this[int i] {
                get { return list[i]; }
                set { list[i] = value; }
            }
        }

        [Serializable]
        public class InteractionMapping {
            public int Interaction = 0;
            public int ModeMask = 0;
            public int ModeMask2 = 0;
            public string InteractionHandler = "None";

            [NonSerialized]
            public VRInteraction InteractionHandlerInstance = null;
        }
    }
    

    [CustomPropertyDrawer(typeof(VRInteractor.InteractionMappingList))]
    public class InteractionMappingListDrawer : PropertyDrawer {
        ReorderableList list;
        VRInteractor interactor;
        VRInteractor.InteractionMappingList realproperty;
        string[] modeNames;
        string[] interactionNames;
        int[] interactionVals;
        Vector2 labelSize;
        List<string> handlerNames = new List<string>();
        bool dirty;
        int lastsize = 0;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Local Modes");
        static readonly GUIContent Mode2SelectLabel = new GUIContent("Remote Modes");
        static readonly GUIContent InteractionSelectLabel = new GUIContent("Interaction");
        static readonly GUIContent HandlerSelectLabel = new GUIContent("Handler");

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                interactor = (VRInteractor)(property.serializedObject.targetObject);
                realproperty = (VRInteractor.InteractionMappingList)fieldInfo.GetValue(property.serializedObject.targetObject);
                lastsize = realproperty.list.Count;
                list = new ReorderableList(realproperty.list, typeof(ModeFilter2), true, true, true, true);
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

                modeNames = VRInteractionAggregator.Global.Modes.Values;
                interactionNames = VRInteractionAggregator.Global.InteractionTypes.abreviatedNames;
                interactionVals = VRInteractionAggregator.Global.InteractionTypes.abreviatedValues;

                handlerNames.Clear();
                handlerNames.Add("None");
                VRInteraction[] interactionComponents = interactor.GetComponents<VRInteraction>();
                foreach (VRInteraction interactionComponent in interactionComponents) {
                    Type componentType = interactionComponent.GetType();
                    if (!handlerNames.Contains(componentType.Name))
                        handlerNames.Add(componentType.Name);
                }

            }
            return list;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            dirty = false;
            realproperty = (VRInteractor.InteractionMappingList)fieldInfo.GetValue(property.serializedObject.targetObject);

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            labelSize = GUI.skin.label.CalcSize(Mode2SelectLabel);

            getList(property);

            EditorGUI.BeginProperty(position, label, property);
            property.serializedObject.ApplyModifiedProperties();
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
            VRInteractor.InteractionMapping mapping = realproperty[index];
            rect.y += 2;

            Vector2 spacerSize = new Vector2(labelSize.x / 2, rect.height);
            Vector2 selectorSize = new Vector2((rect.width - (labelSize.x * 2 + spacerSize.x)) / 2, rect.height);
            Vector2 position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), InteractionSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.Interaction, interactionNames, interactionVals);
            dirty |= (newIntVal != mapping.Interaction);
            mapping.Interaction = newIntVal;

            position.x += selectorSize.x + spacerSize.x;

            EditorGUI.LabelField(new Rect(position, labelSize), HandlerSelectLabel);
            position.x += labelSize.x;
            int handlerIndex = (handlerNames.Contains(mapping.InteractionHandler)) ? handlerNames.IndexOf(mapping.InteractionHandler) : 0;
            newIntVal = EditorGUI.Popup(new Rect(position, selectorSize), handlerIndex, handlerNames.ToArray());
            dirty |= (newIntVal != handlerIndex);
            mapping.InteractionHandler = handlerNames[newIntVal];

            rect.y += rect.height;
            position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ModeSelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.ModeMask, modeNames);
            dirty |= (newIntVal != mapping.ModeMask);
            mapping.ModeMask = newIntVal;

            position.x += selectorSize.x + spacerSize.x;

            EditorGUI.LabelField(new Rect(position, labelSize), Mode2SelectLabel);
            position.x += labelSize.x;
            newIntVal = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.ModeMask2, modeNames);
            dirty |= (newIntVal != mapping.ModeMask2);
            mapping.ModeMask2 = newIntVal;

        }
    }//*/

    /*[CustomEditor(typeof(VRInteractor))]
    public class VRInteractorEditor : Editor {
        VRInteractor interactor;
        ReorderableList list;
        List<string> handlerNames = new List<string>();
        string[] modeNames;
        string[] interactionNames;
        int[] interactionVals;
        Rect nextRect;

        Vector2 labelSize;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Local Modes");
        static readonly GUIContent Mode2SelectLabel = new GUIContent("Remote Modes");
        static readonly GUIContent InteractionSelectLabel = new GUIContent("Interaction");
        static readonly GUIContent HandlerSelectLabel = new GUIContent("Handler");

        private void OnEnable() {
            interactor = (VRInteractor)target;

            list = new ReorderableList(serializedObject,
                    serializedObject.FindProperty("interactionMappings"),
                    true, true, true, true);
            list.drawElementCallback = DrawMapping;
            list.elementHeight = 16 * 2 + 4;
            //list.headerHeight = 32;
            list.draggable = false;

            list.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Interaction Mappings");
            };
            //list.elementHeightCallback = (int index) => { return (index == 0) ? 36 : 18; };

            modeNames = VRInteractionAggregator.Global.Modes.Values;
            interactionNames = VRInteractionAggregator.Global.InteractionTypes.abreviatedNames;
            interactionVals = VRInteractionAggregator.Global.InteractionTypes.abreviatedValues;

            handlerNames.Clear();
            handlerNames.Add("None");
            VRInteraction[] interactionComponents = interactor.GetComponents<VRInteraction>();
            foreach (VRInteraction interactionComponent in interactionComponents) {
                Type componentType = interactionComponent.GetType();
                if (!handlerNames.Contains(componentType.Name))
                    handlerNames.Add(componentType.Name);
            }
        }

        private void OnDisable() {

        }

        public override void OnInspectorGUI() {
            labelSize = GUI.skin.label.CalcSize(Mode2SelectLabel);

            serializedObject.Update();
            list.DoLayoutList();
            interactor._mode = EditorGUILayout.IntField("Mode", interactor._mode);

            //while (true)
            serializedObject.ApplyModifiedProperties();
            if (GUI.changed) {
                EditorUtility.SetDirty(serializedObject.targetObject);
                EditorApplication.MarkSceneDirty();
            }
        }

        void DrawMapping(Rect rect, int index, bool isActive, bool isFocused) {
            rect.height = 16;
            VRInteractor.InteractionMapping mapping = interactor.interactionMappings[index];
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            Vector2 spacerSize = new Vector2(labelSize.x / 2, rect.height);
            Vector2 selectorSize = new Vector2((rect.width - (labelSize.x * 2 + spacerSize.x)) / 2, rect.height);
            Vector2 position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), InteractionSelectLabel);
            position.x += labelSize.x;
            mapping.Interaction = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.Interaction, interactionNames, interactionVals);

            position.x += selectorSize.x + spacerSize.x;

            EditorGUI.LabelField(new Rect(position, labelSize), HandlerSelectLabel);
            position.x += labelSize.x;
            int handlerIndex = (handlerNames.Contains(mapping.InteractionHandler)) ? handlerNames.IndexOf(mapping.InteractionHandler) : 0;
            mapping.InteractionHandler = handlerNames[EditorGUI.Popup(new Rect(position, selectorSize), handlerIndex, handlerNames.ToArray())];

            rect.y += rect.height;
            position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ModeSelectLabel);
            position.x += labelSize.x;
            mapping.ModeMask = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.ModeMask, modeNames);

            position.x += selectorSize.x + spacerSize.x;

            EditorGUI.LabelField(new Rect(position, labelSize), Mode2SelectLabel);
            position.x += labelSize.x;
            mapping.ModeMask2 = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.ModeMask2, modeNames);
        }
    }//*/




    /*[CustomPropertyDrawer(typeof(InteractionMappingList))]
    public class InteractionMappingListDrawer : PropertyDrawer {
        ReorderableList list;
        InteractionMappingList realproperty;
        string[] handlerNames;
        string[] modeNames;
        string[] actionNames;
        string[] interactionNames;
        int[] interactionVals;

        private void OnEnable() {

        }

        ReorderableList getList(SerializedProperty property) {
            if (list == null) {
                InteractionMappingList realproperty = (InteractionMappingList)fieldInfo.GetValue(property.serializedObject.targetObject);
                list = new ReorderableList(realproperty.mappings, typeof(InteractionMappingList.Mapping), true, true, true, true);
                //list = new ReorderableList(property.serializedObject, property, true, true, true, true);
                list.drawElementCallback = DrawMapping;
                //list.elementHeight = 16 * 2 ;
                //list.headerHeight = 32;
                list.draggable = false;

                list.drawHeaderCallback = (Rect rect) => {
                    EditorGUI.LabelField(rect, "Interaction Mappings");
                };
                list.elementHeightCallback = (int index) => {
                    return (index == 0) ? 36 : 18;
                };
            }
            return list;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            realproperty = (InteractionMappingList)fieldInfo.GetValue(property.serializedObject.targetObject);

            getList(property);

            modeNames = VRInteractionManager.Global.Modes.Values;
            actionNames = VRInteractionManager.Global.Actions.Values;
            interactionNames = VRInteractionManager.Global.InteractionTypes.abreviatedNames;
            interactionVals = VRInteractionManager.Global.InteractionTypes.abreviatedValues;

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
            rect.width /= 3;

            if (index == 0) {
                Rect row = rect;
                EditorGUI.LabelField(row, "Interaction");
                row.x += row.width;
                EditorGUI.LabelField(row, "Actions");
                row.x += row.width;
                EditorGUI.LabelField(row, "Modes");
                rect.y += row.height;
            }

            rect.y += 2;

            realproperty.mappings[index].Interaction = EditorGUI.IntPopup(rect, realproperty.mappings[index].Interaction, interactionNames, interactionVals);
            rect.x += rect.width;
            realproperty.mappings[index].ActionMask = EditorGUI.MaskField(rect, realproperty.mappings[index].ActionMask, actionNames);
            rect.x += rect.width;
            realproperty.mappings[index].ModeMask = EditorGUI.MaskField(rect, realproperty.mappings[index].ModeMask, modeNames);
        }
    }//*/

}