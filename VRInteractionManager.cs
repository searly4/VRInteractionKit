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

namespace VRIK {

    public abstract class VRInteractionManager : MonoBehaviour {

        protected List<IActionSource> actionSources = new List<IActionSource>();
        //protected List<VRInteractor> interactors = new List<VRInteractor>();
        protected Dictionary<VRInteractor, IActionSource> actionSourceMap = new Dictionary<VRInteractor, IActionSource>();

        // Use this for initialization
        protected void Start() {
        }

        protected void OnDestroy() {
        }

        protected void OnEnable() {
            VRInteractionAggregator.instance.RegisterManager(this);

            VRInteractionAggregator.instance.RegisteredActionSource += OnRegisteredActionSource;
            VRInteractionAggregator.instance.UnregisteredActionSource += OnUnregisteredActionSource;
            foreach (IActionSource source in VRInteractionAggregator.instance.ActionSources) {
                RegisterActionSource(source);
            }
        }

        protected void OnDisable() {
            if (VRInteractionAggregator.instance != null) {
                VRInteractionAggregator.instance.UnregisterManager(this);
                VRInteractionAggregator.instance.RegisteredActionSource -= OnRegisteredActionSource;
                VRInteractionAggregator.instance.UnregisteredActionSource -= OnUnregisteredActionSource;
            }

            while (actionSources.Count > 0) {
                UnregisterActionSource(actionSources[0]);
            }
        }


        protected virtual void RegisterActionSource(IActionSource source) {
            if (source == null || actionSources.Contains(source))
                return;
            actionSources.Add(source);
            source.InteractorAdded += RegisterInteractor;
            source.InteractorRemoved += UnregisterInteractor;
            source.ActionEvent += OnActionEvent;
            source.InteractionStarted += OnControllerInteractionStarted;
            foreach (var interactor in source.LocalInteractors) {
                RegisterInteractor(source, interactor);
            }
            //source.ActionEvent += OnActionEvent;
        }

        protected virtual void UnregisterActionSource(IActionSource source) {
            if (!actionSources.Contains(source))
                return;
            actionSources.Remove(source);
            source.InteractorAdded -= RegisterInteractor;
            source.InteractorRemoved -= UnregisterInteractor;
            source.ActionEvent -= OnActionEvent;
            source.InteractionStarted -= OnControllerInteractionStarted;
            foreach (var interactor in source.LocalInteractors) {
                UnregisterInteractor(source, interactor);
            }
            //source.ActionEvent -= OnActionEvent;
        }

        protected virtual void RegisterInteractor(IActionSource source, VRInteractor interactor) {
            if (source == null || actionSourceMap.ContainsKey(interactor))
                return;
            //interactors.Add(interactor);
            actionSourceMap[interactor] = source;
            interactor.TouchStarted += OnTouchStarted;
        }

        protected virtual void UnregisterInteractor(IActionSource source, VRInteractor interactor) {
            if (!actionSourceMap.ContainsKey(interactor))
                return;
            //interactors.Remove(interactor);
            actionSourceMap.Remove(interactor);
            interactor.TouchStarted -= OnTouchStarted;
        }

        protected abstract void OnControllerInteractionStarted(IActionSource source, InteractionInstanceTracker tracker);

        private void OnRegisteredActionSource(object sender, ValueEventArgs<IActionSource> e) {
            RegisterActionSource(e.Value);
        }

        private void OnUnregisteredActionSource(object sender, ValueEventArgs<IActionSource> e) {
            UnregisterActionSource(e.Value);
        }

        protected virtual void OnTouchStarted(VRInteractor source, TouchTracker ttracker) {
        }

        protected virtual void OnTouchStopped(VRInteractor source, TouchTracker ttracker) {
        }

        protected virtual void OnActionEvent(ActionTracker tracker) {
        }
    }
}
