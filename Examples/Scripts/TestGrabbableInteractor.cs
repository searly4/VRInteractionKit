using UnityEngine;
using System.Collections;
using VRIK;
using System;

public class TestGrabbableInteractor : MonoBehaviour {
    VRInteractor interactor;
    TestInteractionManager manager;

    public bool isGrabbed { get; private set; }

	// Use this for initialization
	void Start () {
        interactor = GetComponent<VRInteractor>();
        manager = VRInteractionAggregator.instance.GetComponent<TestInteractionManager>();
        interactor.TouchStarted += OnTouchStarted;
        interactor.InteractionStarted += OnInteractionStarted;
    }

    void OnDestroy() {
        interactor.TouchStarted -= OnTouchStarted;
        interactor.InteractionStarted -= OnInteractionStarted;
    }

    private void OnTouchStarted(VRInteractor source, TouchTracker tracker) {
    }

    private void OnInteractionStarted(VRInteractor source, InteractionInstanceTracker tracker) {
        if (tracker.itracker.interactionIndex == manager.grabInteraction.Value) {
            tracker.Released += OnInteractionStopped;
            isGrabbed = true;
        }
    }

    private void OnInteractionStopped(InteractionInstanceTracker tracker) {
        tracker.Released -= OnInteractionStopped;

        if (tracker.itracker.interactionIndex == manager.grabInteraction.Value) {
            isGrabbed = false;
        }
    }
}
