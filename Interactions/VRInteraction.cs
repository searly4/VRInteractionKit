using UnityEngine;
using System.Collections;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace VRIK {
    public class VRInteraction : MonoBehaviour {
        //public ListSelector interactionType = new ListSelector<InteractionTypesSource>();

        protected List<InteractionInstanceTracker> trackerList = new List<InteractionInstanceTracker>();
        protected VRInteractor parentInteractor = null;

        public bool isInteracting { get; protected set; }
        public bool isBinary { get; protected set; }
        public bool isPersistent { get; protected set; }

        public VRInteraction() {
            isInteracting = false;
            isBinary = false;
            isPersistent = false;
        }

        // Use this for initialization
        protected void Start() {
            
        }

        void OnEnable() {
            parentInteractor = GetComponent<VRInteractor>();
            if (parentInteractor != null) {
                parentInteractor.RegisterHandler(this);
            }
        }

        void OnDisable() {
            if (parentInteractor != null) {
                parentInteractor.UnregisterHandler(this);
            }
            parentInteractor = null;
            foreach (InteractionInstanceTracker tracker in trackerList) {
                ReleaseTracker(tracker);
            }
        }

        /// <summary>
        /// Subscribes to state change events from the specified InteractionTracker.
        /// </summary>
        /// <param name="tracker"></param>
        /// <param name="otherInteractor"></param>
        /// <returns></returns>
        protected virtual InteractionInstanceTracker RegisterTracker(InteractionTracker tracker, VRInteractor otherInteractor) {
            InteractionInstanceTracker t = new InteractionInstanceTracker(tracker, parentInteractor, this, otherInteractor);

            trackerList.Add(t);
            t.Released += OnTrackerReleasedEvent;
            tracker.Accept(new InteractionInstanceTracker(tracker, GetComponent<VRInteractor>(), this, otherInteractor));

            return t;
        }

        private void OnTrackerReleasedEvent(InteractionInstanceTracker tracker) {
            ReleaseTracker(tracker);
        }

        /// <summary>
        /// Releases a previously registered InteractionTracker.
        /// </summary>
        /// <param name="tracker"></param>
        protected virtual void ReleaseTracker(InteractionInstanceTracker tracker) {
            tracker.Released -= OnTrackerReleasedEvent;
            trackerList.Remove(tracker);
            tracker.Release();
        }

        protected void ReleaseTracker(InteractionTracker tracker) {
            InteractionInstanceTracker[] tlist = trackerList.ToArray();
            foreach (InteractionInstanceTracker t in tlist) {
                if (t.itracker == tracker)
                    ReleaseTracker(t);
            }
        }

        /// <summary>
        /// Check if this handler can perform any actions on the specified Interactor.
        /// </summary>
        /// <param name="otherInteractor"></param>
        /// <returns></returns>
        public virtual bool CanInteract(VRInteractor otherInteractor) {
            return true;
        }

        /// <summary>
        /// Attempt to apply this handler to the specified interactor.
        /// </summary>
        /// <param name="tracker"></param>
        /// <param name="otherInteractor"></param>
        /// <returns>True if the handler did something with the interactor.</returns>
        public virtual InteractionInstanceTracker TryInteract(InteractionTracker tracker, VRInteractor otherInteractor) {
            return null;
        }

        public IList<InteractionInstanceTracker> GetInteractions(InteractionTracker tracker) {
            List<InteractionInstanceTracker> tlist = new List<InteractionInstanceTracker>();
            foreach (InteractionInstanceTracker t in tlist) {
                if (t.itracker == tracker)
                    tlist.Add(t);
            }
            return tlist;
        }

        public IList<InteractionInstanceTracker> GetInteractions(int interactionIndex) {
            List<InteractionInstanceTracker> tlist = new List<InteractionInstanceTracker>();
            foreach (InteractionInstanceTracker t in tlist) {
                if (t.itracker.interactionIndex == interactionIndex)
                    tlist.Add(t);
            }
            return tlist;
        }
    }
}
