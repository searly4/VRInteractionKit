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
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using Valve.VR;
using WorldUI;

namespace VRIK {
    public class VRController_SteamVR : MonoBehaviour, IActionSource {

        #region Serialized data

        [SerializeField]
        public ButtonMappingList buttonMappings = new ButtonMappingList();

        [SerializeField]
        public List<VRInteractor> detachedInteractors = new List<VRInteractor>();

        [SerializeField]
        public ListSelector menuAction = new ListSelector<ActionsSource>();

        #endregion


        List<VRInteractor> attachedInteractors = new List<VRInteractor>();
        int _mode = 0;

        SteamVR_TrackedObject.EIndex controllerIndex;
        SteamVR_TrackedObject trackedObject;
        SteamVR_Controller.Device controllerDevice;

        WorldTouch worldTouch;

        SortedDictionary<int, ActionTracker> actionTrackers = new SortedDictionary<int, ActionTracker>();

        ulong lastButtonState = 0;

        public event StateChangeEventHandler StateChanged;
        public event ModeChangeEventHandler ModeChanged;
        public event ActionEventHandler ActionEvent;
        public event AttachedInteractorEventHandler InteractorAdded;
        public event AttachedInteractorEventHandler InteractorRemoved;
        public event ActionSourceInteractionEventHandler InteractionStarted;

        public VRInteractor[] LocalInteractors { get { return attachedInteractors.ToArray(); } }

        public bool IsEnabled { get { return gameObject.activeSelf; } }

        public int Mode {
            get { return _mode; }
            set {
                if (value == _mode)
                    return;
                int oldMode = _mode;
                _mode = value;
                OnModeChanged(oldMode, value);
            }
        }

        public VRController_SteamVR() {
            buttonMappings.buttonSource = new SteamVRButtonSource();
        }

        void Awake() {
            trackedObject = GetComponent<SteamVR_TrackedObject>();
        }

        // Use this for initialization
        void Start() {
            VRInteractionAggregator.instance.RegisterActionSource(this);
            StateChangeEventHandler eh = StateChanged;
            if (eh != null)
                eh(this, EState.Started);

        }

        void OnEnable() {
            UpdateCache();
            lastButtonState = 0;
            StateChangeEventHandler eh = StateChanged;
            if (eh != null)
                eh(this, EState.Enabled);
        }

        void OnDisable() {
            StateChangeEventHandler eh = StateChanged;
            if (eh != null)
                eh(this, EState.Disabled);
            lastButtonState = 0;
        }

        void OnDestroy() {
            StateChangeEventHandler eh = StateChanged;
            if (eh != null)
                eh(this, EState.Destroyed);
        }

        void Update() {
            controllerIndex = trackedObject.index;
            if (controllerIndex == SteamVR_TrackedObject.EIndex.None)
                return;
            controllerDevice = SteamVR_Controller.Input((int)controllerIndex);
            if (controllerDevice == null)
                return;

            // process button state changes

            VRControllerState_t currentState = controllerDevice.GetState();
            VRControllerState_t lastState = controllerDevice.GetPrevState();
            ulong changedButtons = currentState.ulButtonPressed ^ lastState.ulButtonPressed;
            ulong pressedButtons = changedButtons & currentState.ulButtonPressed;
            ulong releasedButtons = changedButtons & lastState.ulButtonPressed;
            int modeMask = 1 << _mode;

            if (changedButtons == 0)
                return;

            foreach (ButtonMapping mapping in buttonMappings.list) {
                bool buttonPressed = (((ulong)1 << mapping.button) & pressedButtons) != 0;
                bool buttonReleased = (((ulong)1 << mapping.button) & releasedButtons) != 0;
                if ((mapping.modeMask & modeMask) == 0 || (!buttonPressed && !buttonReleased))
                    continue;

                ActionTracker tracker = actionTrackers[mapping.action];
                EAction actionType = EAction.None;

                switch (mapping.toggle) {
                    case EActionEdge.Momentary:
                        if (((ActionTracker<bool>)tracker).value && buttonReleased)
                            actionType = EAction.Stop;
                        else if (!((ActionTracker<bool>)tracker).value && buttonPressed)
                            actionType = EAction.Start;
                        break;
                    case EActionEdge.ToggleLeading:
                        if (buttonPressed)
                            actionType = ((ActionTracker<bool>)tracker).value ? EAction.Stop : EAction.Start;
                        break;
                    case EActionEdge.ToggleTrailing:
                        if (buttonReleased)
                            actionType = ((ActionTracker<bool>)tracker).value ? EAction.Stop : EAction.Start;
                        break;
                    case EActionEdge.OneshotLeading:
                        if (buttonPressed)
                            actionType = EAction.Oneshot;
                        break;
                    case EActionEdge.OneshotTrailing:
                        if (buttonReleased)
                            actionType = EAction.Oneshot;
                        break;
                }

                OnAction(mapping, actionType);
            }
        }

        void OnAction(ButtonMapping mapping, EAction actionType) {
            ActionTracker tracker = actionTrackers[mapping.action];

            switch (actionType) {
                case EAction.None:
                    return;
                case EAction.Start:
                    ((ActionTracker<bool>)tracker).value = true;
                    break;
                case EAction.Stop:
                    ((ActionTracker<bool>)tracker).value = false;
                    break;
            }

            if (mapping.action == menuAction.Value && worldTouch != null) {
                worldTouch.SetTriggerState(((ActionTracker<bool>)tracker).value);
            }

            ActionEventHandler eh = ActionEvent;
            if (eh != null)
                eh(tracker);
        }

        private void OnModeChanged(int oldMode, int newMode) {
            foreach (VRInteractor interactor in attachedInteractors) {
                interactor.Mode = newMode;
            }
            ModeChangeEventHandler eh = ModeChanged;
            if (eh != null)
                eh(this, oldMode, newMode);
        }

        public void AddInteractor(VRInteractor interactor) {
            if (attachedInteractors.Contains(interactor))
                return;
            attachedInteractors.Add(interactor);
            interactor.InteractionStarted += OnInteractionStarted;

            AttachedInteractorEventHandler eh = InteractorAdded;
            if (eh != null)
                eh(this, interactor);
        }

        public void RemoveInteractor(VRInteractor interactor) {
            if (!attachedInteractors.Contains(interactor))
                return;
            interactor.InteractionStarted -= OnInteractionStarted;
            attachedInteractors.Remove(interactor);

            AttachedInteractorEventHandler eh = InteractorRemoved;
            if (eh != null)
                eh(this, interactor);
        }

        /// <summary>
        /// Relay the InteractionStarted event from the local interactors.
        /// </summary>
        /// <param name="tracker"></param>
        /// <param name="handler"></param>
        private void OnInteractionStarted(VRInteractor source, InteractionInstanceTracker tracker) {
            ActionSourceInteractionEventHandler eh = InteractionStarted;
            if (eh != null)
                eh(this, tracker);
        }

        void UpdateCache() {
            if (worldTouch == null)
                worldTouch = GetComponent<WorldTouch>();
            if (worldTouch == null)
                worldTouch = GetComponentInChildren<WorldTouch>(true);

            List<VRInteractor> interactors = new List<VRInteractor>(detachedInteractors);
            interactors.AddRange(transform.GetComponentsInChildren<VRInteractor>(true) as ICollection<VRInteractor>);
            foreach (VRInteractor interactor in interactors) {
                if (attachedInteractors.Contains(interactor))
                    continue;
                AddInteractor(interactor);
            }
            foreach (VRInteractor interactor in attachedInteractors) {
                if (interactors.Contains(interactor))
                    continue;
                RemoveInteractor(interactor);
            }

            foreach (ButtonMapping mapping in buttonMappings.list) {
                // todo: cleanup removed mappings (not critical, only effects design time)
                ActionTracker tracker = null;
                actionTrackers.TryGetValue(mapping.action, out tracker);
                
                if (mapping.toggle == EActionEdge.OneshotLeading || mapping.toggle == EActionEdge.OneshotTrailing) {
                    if (tracker != null && !(tracker is OneShotActionTracker)) {
                        tracker.Release();
                        tracker = null;
                    }
                    if (tracker == null)
                        tracker = new OneShotActionTracker(this, mapping.action, mapping.toggle);
                } else {
                    if (tracker != null && !(tracker is ActionTracker<bool>)) {
                        tracker.Release();
                        tracker = null;
                    }
                    if (tracker == null)
                        tracker = new ActionTracker<bool>(this, mapping.action, mapping.toggle, false);
                }
                actionTrackers[mapping.action] = tracker;
            }
        }

        public class SteamVRButtonSource : ButtonMappingList.IButtonSource {
            public int this[string name] {
                get {
                    return (int)Enum.Parse(typeof(Valve.VR.EVRButtonId), name);
                }
            }

            public string[] names {
                get {
                    string[] buttonNames = Enum.GetNames(typeof(Valve.VR.EVRButtonId));
                    for (int i = 0; i < buttonNames.Length; i++) {
                        buttonNames[i] = buttonNames[i].Substring(3);
                        buttonNames[i] = buttonNames[i].Replace('_', ' ');
                    }
                    return buttonNames;
                }
            }

            public int[] values {
                get {
                    return (int[])Enum.GetValues(typeof(Valve.VR.EVRButtonId));
                }
            }
        }
    }

    /*[CustomEditor(typeof(VRController_SteamVR))]
    public class VRController_SteamVREditor : Editor {
        ReorderableList list;
        string[] modeNames;
        string[] buttonNames;
        int[] buttonVals;
        string[] actionNames;
        int[] actionVals;
        Vector2 labelSize;
        //Vector2 selectorSize;
        //Vector2 gapSize;

        static readonly GUIContent ModeSelectLabel = new GUIContent("Modes");
        static readonly GUIContent ButtonSelectLabel = new GUIContent("Button");
        static readonly GUIContent ActionSelectLabel = new GUIContent("Action");
        static readonly GUIContent ActivationSelectLabel = new GUIContent("Activation");

        private void OnEnable() {
            list = new ReorderableList(serializedObject,
                    serializedObject.FindProperty("buttonMappings"),
                    true, true, true, true);
            list.drawElementCallback = DrawMapping;
            list.elementHeight = 16 * 2 + 4;
            //list.headerHeight = 32;
            list.draggable = false;

            list.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Button Mappings");
            };
            //list.elementHeightCallback = (int index) => {
            //    return (index == 0) ? 36 : 18;
            //};

            this.modeNames = VRInteractionAggregator.Global.Modes.Values;

            buttonNames = Enum.GetNames(typeof(Valve.VR.EVRButtonId));
            buttonVals = (int[])Enum.GetValues(typeof(Valve.VR.EVRButtonId));
            for (int i = 0; i < buttonNames.Length; i++) {
                buttonNames[i] = buttonNames[i].Substring(3);
                buttonNames[i] = buttonNames[i].Replace('_', ' ');
            }

            this.actionNames = VRInteractionAggregator.Global.Actions.abreviatedNames;
            this.actionVals = VRInteractionAggregator.Global.Actions.abreviatedValues;
        }

        private void OnDisable() {

        }

        public override void OnInspectorGUI() {
            labelSize = GUI.skin.label.CalcSize(ActivationSelectLabel);

            serializedObject.Update();
            list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawMapping(Rect rect, int index, bool isActive, bool isFocused) {
            rect.height = 16;
            ButtonMapping mapping = ((VRController_SteamVR)target).buttonMappings[index];
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            Vector2 spacerSize = new Vector2(labelSize.x / 2, rect.height);
            Vector2 selectorSize = new Vector2((rect.width - (labelSize.x * 2 + spacerSize.x)) / 2, rect.height);
            Vector2 position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ModeSelectLabel);
            position.x += labelSize.x;
            mapping.modeMask = EditorGUI.MaskField(new Rect(position, selectorSize), mapping.modeMask, modeNames);
            position.x += selectorSize.x + spacerSize.x;
            EditorGUI.LabelField(new Rect(position, labelSize), ButtonSelectLabel);
            position.x += labelSize.x;
            mapping.button = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.button, buttonNames, buttonVals);

            rect.y += rect.height;
            position = new Vector2(rect.x, rect.y);

            EditorGUI.LabelField(new Rect(position, labelSize), ActivationSelectLabel);
            position.x += labelSize.x;
            mapping.toggle = (EActionEdge)EditorGUI.EnumPopup(new Rect(position, selectorSize), mapping.toggle);
            position.x += selectorSize.x + spacerSize.x;
            EditorGUI.LabelField(new Rect(position, labelSize), ActionSelectLabel);
            position.x += labelSize.x;
            mapping.action = EditorGUI.IntPopup(new Rect(position, selectorSize), mapping.action, actionNames, actionVals);
        }
    }//*/
}
