using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;
using VRIK;

namespace VRIK {
    public class TestInteractionManager : VRInteractionManager {
        [SerializeField]
        public ListSelector grabAction = new ListSelector<ActionsSource>();
        [SerializeField]
        public ListSelector grabInteraction = new ListSelector<InteractionTypesSource>();

        [SerializeField]
        public ListSelector useAction = new ListSelector<ActionsSource>();
        [SerializeField]
        public ListSelector useInteraction = new ListSelector<InteractionTypesSource>();

        [SerializeField]
        public ListSelector menuAction = new ListSelector<ActionsSource>();
        public Joint popupMenu;

        [SerializeField]
        public ListSelector busyMode = new ListSelector<ModesSource>();
        [SerializeField]
        public ListSelector canGrabMode = new ListSelector<ModesSource>();
        [SerializeField]
        public ListSelector secondaryMode = new ListSelector<ModesSource>();
        [SerializeField]
        public ListSelector grabbedMode = new ListSelector<ModesSource>();
        [SerializeField]
        public ListSelector touchingMode = new ListSelector<ModesSource>();


        /// <summary>
        /// 
        /// </summary>
        protected Dictionary<TouchTracker, InteractionTracker<bool>> touchTrackerList = new Dictionary<TouchTracker, InteractionTracker<bool>>();

        /// <summary>
        /// 
        /// </summary>
        protected Dictionary<ActionTracker, InteractionTracker> actionTrackerList = new Dictionary<ActionTracker, InteractionTracker>();

        /// <summary>
        /// 
        /// </summary>
        protected Dictionary<int, InteractionTracker> actionList = new Dictionary<int, InteractionTracker>();

        /// <summary>
        /// 
        /// </summary>
        Dictionary<VRHighlightInteraction, TouchList> highlightTouchList = new Dictionary<VRHighlightInteraction, TouchList>();

        protected Dictionary<VRInteractor, List<InteractionTracker>> interactionList = new Dictionary<VRInteractor, List<InteractionTracker>>();

        protected Dictionary<InteractionTracker, IActionSource> interactionTrackerMap = new Dictionary<InteractionTracker, IActionSource>();

        ControllerList controllerList = new ControllerList();

        protected new void Start() {
            base.Start();
        }

        protected new void OnDestroy() {
            base.OnDestroy();
        }

        protected new void OnEnable() {
            base.OnEnable();
        }

        protected new void OnDisable() {
            base.OnDisable();
        }

        protected override void RegisterActionSource(IActionSource source) {
            controllerList.Add(source);
            base.RegisterActionSource(source);
        }

        protected override void UnregisterActionSource(IActionSource source) {
            controllerList.Remove(source);
            base.UnregisterActionSource(source);
        }

        protected override void OnTouchStarted(VRInteractor source, TouchTracker ttracker) {
            VRInteractor target = ttracker.GetOther(source);
            if (actionSourceMap.ContainsKey(target))
                return; // ignore other controllers

            /*VRHighlightInteraction highlight = source.GetComponent<VRHighlightInteraction>();
            if (highlight != null)
                StartHighlight(source, highlight, target, ttracker);

            highlight = target.GetComponent<VRHighlightInteraction>();
            if (highlight != null)
                StartHighlight(target, highlight, source, ttracker);//*/

            IActionSource controller = actionSourceMap[source];

            bool track = false;

            if (SetObjectMode(target))
                track = true;
            if (controller != null && SetControllerMode(controller))
                track = true;
            if (track)
                ttracker.Released += OnTouchTrackerReleased;
        }

        /// <summary>
        /// Analyze controller state and set it to an appropriate mode.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected bool SetControllerMode(IActionSource controller) {
            ControllerTracker ctracker = controllerList[controller];

            bool secondary = false;
            foreach (ControllerTracker c in controllerList) {
                if (c == ctracker)
                    continue;
                if (c.grabbedInteractor != null)
                    secondary = true;
            }

            if (ctracker.grabbedInteractor != null) {
                controller.Mode = busyMode.Value;
                return true;
            }

            // build list of all touched objects associated with selected controller
            List<VRInteractor> interactors = new List<VRInteractor>();
            foreach (VRInteractor i in controller.LocalInteractors) {
                interactors.AddRange(i.touchedObjects);
            }

            foreach (VRInteractor i in interactors) {
                if (i.Mode == grabbedMode.Value) {
                    controller.Mode = canGrabMode.Value;
                    return true;
                }

                if (i.Mode != secondaryMode.Value && !i.HasInteraction(grabInteraction.Value))
                    continue;

                controller.Mode = canGrabMode.Value;
                return true;
            }

            controller.Mode = 0;

            return false;
        }

        protected void SetControllerMode() {
            // see what each controller is up to
            foreach (ControllerTracker ctracker in controllerList) {
            }

            /*if (interactionTrackerMap[tracker] == actionSource)
                actionSource.Mode = busyMode.Value;
            else
                actionSource.Mode = secondaryMode.Value;//*/

            foreach (IActionSource actionSource in actionSources) {
                SetControllerMode(actionSource);
            }
        }

        protected bool SetObjectMode(VRInteractor interactor) {
            int nextMode = interactor.Mode;

            if (IsGrabbed(interactor))
                nextMode = grabbedMode.Value;
            else if (interactor.parent != null && interactor.parent.Mode == grabbedMode.Value)
                nextMode = secondaryMode.Value;
            else if (IsTouching(interactor))
                nextMode = touchingMode.Value;
            else
                nextMode = 0;

            if (nextMode != interactor.Mode) {
                interactor.Mode = nextMode;
                return true;
            }
            return false;
        }

        protected bool IsGrabbed(VRInteractor interactor) {
            InteractionInstanceTracker[] interactions = interactor.interactions;
            foreach (InteractionInstanceTracker tracker in interactions) {
                if (tracker.itracker.interactionIndex == grabInteraction.Value && tracker.handler != null && tracker.otherInteractor == interactor)
                    return true;
            }
            return false;
        }

        protected bool IsTouching(VRInteractor interactor) {
            VRInteractor[] interactions = interactor.GetTouching();
            foreach (VRInteractor touchedInteractor in interactions) {
                if (actionSourceMap.ContainsKey(touchedInteractor))
                    return true;
            }
            return false;
        }

        protected void OnTouchTrackerReleased(VRInteractor source, TouchTracker ttracker) {
            ttracker.Released -= OnTouchTrackerReleased;
            IActionSource controller;

            if (actionSourceMap.TryGetValue(ttracker.A, out controller)) {
                SetControllerMode(controller);
            } else {
                SetObjectMode(ttracker.A);
            }

            if (actionSourceMap.TryGetValue(ttracker.B, out controller)) {
                SetControllerMode(controller);
            } else {
                SetObjectMode(ttracker.B);
            }
        }

        protected void StartHighlight(VRInteractor target, VRHighlightInteraction highlight, VRInteractor other, TouchTracker ttracker) {
            TouchList targetTouchList;
            if (!highlightTouchList.TryGetValue(highlight, out targetTouchList)) {
                InteractionTracker<bool> itracker = new InteractionTracker<bool>(0, true, false, true);
                if (highlight.TryInteract(itracker, null) == null)
                    return;
                targetTouchList = new TouchList { Tracker = itracker };
                highlightTouchList[highlight] = targetTouchList;
            }
            targetTouchList.Interactors.Add(other);
            ttracker.Released += StopHighlight;
        }

        protected void StopHighlight(VRInteractor source, TouchTracker ttracker) {
            ttracker.Released -= StopHighlight;
            //VRInteractor target = ttracker.GetOther(source);

            VRHighlightInteraction highlight = ttracker.B.GetComponent<VRHighlightInteraction>();
            if (highlight != null)
                StopHighlight(ttracker.A, highlight, ttracker.B);

            highlight = ttracker.A.GetComponent<VRHighlightInteraction>();
            if (highlight != null)
                StopHighlight(ttracker.B, highlight, ttracker.A);
        }

        protected void StopHighlight(VRInteractor target, VRHighlightInteraction highlight, VRInteractor other) {
            TouchList targetTouchList;
            if (!highlightTouchList.TryGetValue(highlight, out targetTouchList))
                return;

            if (targetTouchList.Interactors.Contains(target))
                targetTouchList.Interactors.Remove(target);

            if (targetTouchList.Interactors.Count > 0)
                return;

            targetTouchList.Tracker.Release();
            highlightTouchList.Remove(highlight);
        }

        protected override void OnActionEvent(ActionTracker tracker) {
            int interactionIndex = -1;
            if (tracker.actionIndex == menuAction.Value) {
                if (tracker is ActionTracker<bool>) {
                    ActionTracker<bool> booltracker = tracker as ActionTracker<bool>;
                    GameObject sourcego = booltracker.source.LocalInteractors[0].gameObject;
                    Rigidbody rb = sourcego.GetComponent<Rigidbody>();
                    if (booltracker.value) {
                        popupMenu.transform.position = rb.transform.position;
                        popupMenu.transform.rotation = rb.transform.rotation;
                        popupMenu.connectedBody = rb;
                        popupMenu.gameObject.SetActive(true);

                    } else {
                        popupMenu.connectedBody = null;
                        popupMenu.gameObject.SetActive(false);
                    }
                }
                return;
            }

            // map controller actions to interactions
            if (tracker.actionIndex == grabAction.Value) {
                interactionIndex = grabInteraction.Value;
            }
            if (tracker.actionIndex == useAction.Value) {
                interactionIndex = useInteraction.Value;
            }

            if (interactionIndex < 0)
                return;

            if (tracker is ActionTracker<bool>) {
                ActionTracker<bool> booltracker = (ActionTracker<bool>)tracker;
                InteractionTracker<bool> boolitracker = null;
                InteractionTracker itracker = null;

                if (actionTrackerList.TryGetValue(booltracker, out itracker)) {
                    if (!booltracker.value)
                        actionTrackerList.Remove(booltracker);
                } else if (actionList.TryGetValue(booltracker.actionIndex, out itracker)) {
                    if (!booltracker.value)
                        actionList.Remove(booltracker.actionIndex);
                }

                if (itracker != null) {
                    boolitracker = itracker as InteractionTracker<bool>;
                    boolitracker.value = booltracker.value;

                    if (!booltracker.value) {
                        itracker.Release();
                        boolitracker = null;
                    }
                } else if (booltracker.value) {
                    boolitracker = new InteractionTracker<bool>(interactionIndex, booltracker.value, false, true);
                    //boolitracker.Released += OnTrackerReleased;
                    //if (interactionIndex == grabInteraction.Value)
                    if (tracker.actionEdge == EActionEdge.ToggleLeading || tracker.actionEdge == EActionEdge.ToggleTrailing)
                        actionList[booltracker.actionIndex] = boolitracker;
                    else
                        actionTrackerList[booltracker] = boolitracker;

                    if (interactionIndex == grabInteraction.Value) {
                        //itracker.Accepted += OnGrab;
                        //itracker.Released += OnGrabTrackerReleased;
                    }
                }
                //interactionTrackerMap[boolitracker] = tracker.source;
                //boolitracker.Released += OnTrackerReleased;

                if (boolitracker != null) {
                    IActionSource source = booltracker.source;
                    VRInteractor[] interactors = source.LocalInteractors;

                    //atracker.Released += OnActionStopped;
                    foreach (VRInteractor interactor in interactors) {
                        interactor.DispatchAll(boolitracker);
                    }
                }
            }
        }

        void OnTrackerReleased(InteractionTracker tracker) {
            tracker.Released -= OnTrackerReleased;
            //actionList.Remove(booltracker.actionIndex);
            //interactionTrackerMap.Remove(tracker);
        }

        void OnGrab(InteractionTracker tracker, VRInteraction handler) {
            return;
            if (!(handler is VRGrabInteraction))
                return;
            VRInteractor handlerInteractor = handler.GetComponent<VRInteractor>();
            ControllerTracker ctracker = controllerList[handlerInteractor];
            if (ctracker != null) {
                //ctracker.grabbedInteractor = tracker.otherInteractor;
            } else {
                //ctracker = controllerList[tracker.otherInteractor];
                ctracker.grabbedInteractor = handlerInteractor;
            }

            SetControllerMode();
        }

        private void OnInteractionReleased(InteractionInstanceTracker tracker) {
            foreach (ControllerTracker ctracker in controllerList) {
                if (ctracker.LocalInteractors.Contains(tracker.interactor))
                    SetObjectMode(tracker.otherInteractor);
                if (ctracker.LocalInteractors.Contains(tracker.otherInteractor))
                    SetObjectMode(tracker.interactor);
            }
        }

        protected override void OnControllerInteractionStarted(IActionSource source, InteractionInstanceTracker tracker) {
            ControllerTracker ctracker = controllerList[source];
            tracker.Released += OnControllerInteractionStopped;

            if (tracker.itracker.interactionIndex == grabInteraction.Value) {
                ctracker.grabbedInteractor = (source.LocalInteractors.Contains(tracker.interactor)) ? tracker.otherInteractor : tracker.interactor;
                ctracker.grabTracker = tracker;
                tracker.Released += OnInteractionReleased;
                SetObjectMode(ctracker.grabbedInteractor);
            }
            SetControllerMode();
        }

        private void OnControllerInteractionStopped(InteractionInstanceTracker tracker) {
            tracker.Released -= OnControllerInteractionStopped;
            foreach (ControllerTracker ctracker in controllerList) {
                if (ctracker.grabTracker == tracker && tracker.itracker.interactionIndex == grabInteraction.Value) {
                    ctracker.grabbedInteractor = null;
                    ctracker.grabTracker = null;
                }
            }
            SetControllerMode();
        }

        class TouchList {
            public List<VRInteractor> Interactors = new List<VRInteractor>();
            public InteractionTracker<bool> Tracker;
        }

        class ControllerList : List<ControllerTracker> {
            public ControllerTracker this[IActionSource actionSource] {
                get {
                    foreach (ControllerTracker tracker in this) {
                        if (tracker.controller == actionSource)
                            return tracker;
                    }
                    return null;
                }
            }
            public ControllerTracker this[VRInteractor interactor] {
                get {
                    foreach (ControllerTracker tracker in this) {
                        if (tracker.controller.LocalInteractors.Contains(interactor))
                            return tracker;
                    }
                    return null;
                }
            }
            public void Add(IActionSource actionSource) {
                if (this[actionSource] != null)
                    return;
                Add(new ControllerTracker(actionSource));
            }
            public void Remove(IActionSource actionSource) {
                ControllerTracker tracker = this[actionSource];
                if (tracker == null)
                    return;
                Remove(tracker);
            }
        }

        class ControllerTracker {
            public IActionSource controller;
            public VRInteractor grabbedInteractor = null;
            public InteractionInstanceTracker grabTracker = null;

            public VRInteractor[] LocalInteractors { get { return controller.LocalInteractors; } }

            public VRInteractor[] TouchedInteractors {
                get {
                    List<VRInteractor> interactors = new List<VRInteractor>();
                    foreach (VRInteractor i in controller.LocalInteractors) {
                        interactors.AddRange(i.touchedObjects);
                    }
                    return interactors.ToArray();
                }
            }

            public ControllerTracker(IActionSource actionSource) {
                controller = actionSource;
            }
        }
    }
}
