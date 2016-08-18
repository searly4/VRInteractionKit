using UnityEngine;
using System.Collections;
using System;

namespace VRIK {
    public class VRScaleInteraction : VRInteraction {
        public VRInteractor grabbedInteractor;

        VRInteractor manipulator;
        float initialDistance;
        Vector3 initialScale;

        public VRScaleInteraction() : base() {
            isPersistent = true;
        }

        // Use this for initialization
        new void Start() {
            base.Start();
        }

        void Update() {
            if (manipulator == null)
                return;

            float distance = (grabbedInteractor.transform.position - manipulator.transform.position).magnitude;
            float scaleFactor = distance / initialDistance;
            grabbedInteractor.transform.localScale = initialScale * scaleFactor;

        }

        protected override InteractionInstanceTracker RegisterTracker(InteractionTracker tracker, VRInteractor otherInteractor) {
            InteractionTracker<bool> booltracker = tracker as InteractionTracker<bool>;
            if (booltracker == null)
                return null;
            booltracker.ValueChanged += OnTrackerValueChanged;

            InteractionInstanceTracker t = base.RegisterTracker(tracker, otherInteractor);
            return t;
        }

        private void OnTrackerValueChanged(InteractionTracker tracker, bool value) {
            if (value)
                return;
            StopScale(null);
            ReleaseTracker(tracker);
        }

        protected override void ReleaseTracker(InteractionInstanceTracker tracker) {
            base.ReleaseTracker(tracker);
        }

        public override InteractionInstanceTracker TryInteract(InteractionTracker tracker, VRInteractor otherInteractor) {
            InteractionTracker<bool> booltracker = tracker as InteractionTracker<bool>;
            if (!booltracker.value || !StartScale(otherInteractor))
                return null;

            return RegisterTracker(tracker, otherInteractor);
        }

        bool StartScale(VRInteractor otherInteractor) {
            manipulator = otherInteractor;
            initialDistance = (grabbedInteractor.transform.position - manipulator.transform.position).magnitude;
            initialScale = grabbedInteractor.transform.localScale;
            
            return true;
        }

        void StopScale(VRInteractor otherInteractor) {
            manipulator = null;
        }
    }
}
