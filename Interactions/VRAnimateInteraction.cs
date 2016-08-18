using UnityEngine;
using System.Collections;
using System;

namespace VRIK {
    public class VRAnimateInteraction : VRInteraction {
        public Animator animator;
        public VRInteractor grabbedInteractor;

        VRInteractor manipulator;
        float initialDistance;
        Vector3 initialScale;

        public VRAnimateInteraction() : base() {
            isPersistent = true;
        }

        // Use this for initialization
        new void Start() {
            base.Start();
        }

        void OnEnable() {
            if (grabbedInteractor == null)
                grabbedInteractor = GetComponentInParent<VRInteractor>();
            animator.speed = 0;
        }

        void Update() {
            if (manipulator == null)
                return;

            float distance = (grabbedInteractor.transform.position - manipulator.transform.position).magnitude;
            float scaleFactor = Mathf.Min((Mathf.Max(distance / initialDistance, 1) - 1) / 4, 1);
            animator.Play("", 0, scaleFactor);
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
            StopAnimate(null);
            ReleaseTracker(tracker);
        }

        protected override void ReleaseTracker(InteractionInstanceTracker tracker) {
            base.ReleaseTracker(tracker);
        }

        public override InteractionInstanceTracker TryInteract(InteractionTracker tracker, VRInteractor otherInteractor) {
            InteractionTracker<bool> booltracker = tracker as InteractionTracker<bool>;
            if (!booltracker.value || !StartAnimate(otherInteractor))
                return null;

            return RegisterTracker(tracker, otherInteractor);
        }

        bool StartAnimate(VRInteractor otherInteractor) {
            manipulator = otherInteractor;
            initialDistance = (grabbedInteractor.transform.position - manipulator.transform.position).magnitude;
            initialScale = grabbedInteractor.transform.localScale;
            
            return true;
        }

        void StopAnimate(VRInteractor otherInteractor) {
            manipulator = null;
        }
    }
}
