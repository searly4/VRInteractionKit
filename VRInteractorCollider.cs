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
    /// <summary>
    /// The VRInteractorCollider component relays collision and trigger events to an associated
    /// VRInteractor component. This allows VRInteractor to receive events from multiple colliders.
    /// </summary>
    public class VRInteractorCollider : MonoBehaviour {
        public VRInteractor interactor;

        [SerializeField]
        public ModeFilter2List filters = new ModeFilter2List();

        //protected Dictionary<VRInteractor, int> touchCounts = new Dictionary<VRInteractor, int>();
        //protected List<VRInteractor> filteredTouches = new List<VRInteractor>();
        protected List<ColliderData> touchList = new List<ColliderData>();
        protected List<Collider> colliderList = new List<Collider>();

        void Start() {
            GetInteractor();
        }

        void OnEnable() {
            GetInteractor();
        }

        void OnDisable() {
            List<VRInteractor> oldTouches = new List<VRInteractor>();
            foreach (ColliderData cdata in touchList) {
                if (cdata.touchValid && !oldTouches.Contains(cdata.interactor))
                    oldTouches.Add(cdata.interactor);
            }
            foreach (VRInteractor other in oldTouches) {
                interactor.StoppedTouching(other);
            }

            touchList.Clear();
            colliderList.Clear();
        }

        bool GetInteractor() {
            if (interactor != null)
                return true;
            interactor = GetComponent<VRInteractor>();
            if (interactor == null)
                interactor = GetComponentInParent<VRInteractor>();
            if (interactor == null)
                return false;
            interactor.ModeChanged += OnModeChanged;
            /*if (interactor == null) {
                gameObject.SetActive(false);
                throw new Exception("Could not find VRInteractor.");
            }//*/
            return true;
        }

        private void OnModeChanged(object source, int oldMode, int newMode) {
            VRInteractor interactor = (VRInteractor)source;
            if (interactor == this.interactor) {
                List<VRInteractor> newTouches = new List<VRInteractor>();
                List<VRInteractor> oldTouches = new List<VRInteractor>();

                foreach (ColliderData cdata in touchList) {
                    if (cdata.touchValid && !oldTouches.Contains(cdata.interactor))
                        oldTouches.Add(cdata.interactor);
                    bool newTouchState = CheckFilter(cdata.interactor, cdata.icollider);
                    if (newTouchState && !newTouches.Contains(cdata.interactor))
                        newTouches.Add(cdata.interactor);
                    cdata.SetTouch(newTouchState);
                }

                foreach (VRInteractor other in newTouches) {
                    if (!oldTouches.Contains(other))
                        interactor.StartedTouching(other);
                }
                foreach (VRInteractor other in oldTouches) {
                    if (!newTouches.Contains(other))
                        interactor.StoppedTouching(other);
                }

            } else {
                bool oldTouchState = false;
                bool newTouchState = false;
                foreach (ColliderData cdata in touchList) {
                    if (cdata.interactor != interactor)
                        continue;
                    oldTouchState |= cdata.touchValid;
                    bool ts = CheckFilter(cdata.interactor, cdata.icollider);
                    newTouchState |= ts;
                    cdata.SetTouch(ts);
                }
                if (newTouchState && !oldTouchState)
                    this.interactor.StartedTouching(interactor);
                else if (!newTouchState && oldTouchState)
                    this.interactor.StoppedTouching(interactor);
            }
        }

        bool CheckFilter(VRInteractor otherInteractor, VRInteractorCollider otherCollider) {
            /*List<VRInteractorCollider> otherColliders = new List<VRInteractorCollider>();
            foreach (ColliderData cdata in touchList) {
                if (cdata.interactor == otherInteractor)
                    otherColliders.Add(collider);
            }//*/

            bool retval = true;
            if (filters.list.Count > 0) {
                bool foundMatch = false;
                foreach (ModeFilter2 filter in filters.list) {
                    if ((filter.modeMask & (1 << interactor.Mode)) == 0 || (filter.modeMask2 & (1 << otherInteractor.Mode)) == 0)
                        continue;
                    foundMatch = true;
                    break;
                }
                retval &= foundMatch;
            }

            if (otherCollider.filters.list.Count > 0) {
                bool foundMatch = false;
                foreach (ModeFilter2 otherFilter in otherCollider.filters.list) {
                    if ((otherFilter.modeMask2 & (1 << interactor.Mode)) == 0 || (otherFilter.modeMask & (1 << otherInteractor.Mode)) == 0)
                        continue;
                    foundMatch = true;
                    break;
                }
                retval &= foundMatch;
            }

            return retval;
        }

        void StartedTouching(VRInteractorCollider ic) {
            VRInteractor other = ic.interactor;

            bool touched = false;
            for (int i = 0; i < touchList.Count; i++) {
                if (touchList[i].icollider == ic) {
                    touchList[i].Increment();
                    return;
                }

                // check if we've already sent a StartedTouching message
                if (touchList[i].interactor == other && touchList[i].touchValid)
                    touched = true;
            }


            if (CheckFilter(other, ic)) {
                touchList.Add(new ColliderData(other, ic, true, 1));
                if (!touched)
                    interactor.StartedTouching(other);
            } else {
                touchList.Add(new ColliderData(other, ic, false, 1));
            }
            //Debug.Log("Add touch: " + this.ToString() + " -> " + ic.ToString());
            other.ModeChanged += OnModeChanged;
        }

        void StoppedTouching(VRInteractorCollider ic) {
            VRInteractor other = ic.interactor;

            bool touchedByThis = false;
            bool touchedByOther = false;
            int matchedIndex = -1;
            // find matching ColliderData item and check touch status of the interactor
            for (int i = 0; i < touchList.Count; i++) {
                if (touchList[i].icollider == ic) {
                    if (touchList[i].Decrement() > 0)
                        return;
                    matchedIndex = i;
                    touchedByThis = touchList[i].touchValid;
                } else if (touchList[i].interactor == other && touchList[i].touchValid) {
                    touchedByOther |= touchList[i].touchValid;
                }
            }

            other.ModeChanged -= OnModeChanged;

            //Debug.Log("Remove touch: " + this.ToString() + " -> " + ((matchedIndex < 0) ? ("(" + ic.ToString() + ")") : touchList[matchedIndex].icollider.ToString()));
            touchList.RemoveAt(matchedIndex);

            if (touchedByThis && !touchedByOther)
                interactor.StoppedTouching(other);
        }

        void OnTriggerEnter(Collider collider) {
            if (!isActiveAndEnabled || !GetInteractor() || colliderList.Contains(collider))
                return; // unity often calls triggers twice
            VRInteractorCollider ic = collider.GetComponent<VRInteractorCollider>();

            // ignore objects with no interactor or sharing this object's interactor
            if (ic == null || !ic.isActiveAndEnabled || ic.interactor == null || ic.interactor == interactor)
                return;

            colliderList.Add(collider);
            StartedTouching(ic);
        }

        void OnTriggerExit(Collider collider) {
            if (!isActiveAndEnabled || !GetInteractor() || !colliderList.Contains(collider))
                return;
            VRInteractorCollider ic = collider.GetComponent<VRInteractorCollider>();

            // ignore objects with no interactor or sharing this object's interactor
            if (ic == null || !ic.isActiveAndEnabled || ic.interactor == null || ic.interactor == interactor)
                return;

            StoppedTouching(ic);
            colliderList.Remove(collider);
        }

        protected struct ColliderData : IEquatable<ColliderData> {
            public readonly VRInteractor interactor;
            public readonly VRInteractorCollider icollider;
            public bool touchValid;
            public int colliderCount;

            public ColliderData(VRInteractor interactor, VRInteractorCollider icollider, bool touchValid, int colliderCount) {
                this.interactor = interactor;
                this.icollider = icollider;
                this.touchValid = touchValid;
                this.colliderCount = colliderCount;
            }

            public int Increment() {
                colliderCount++;
                return colliderCount;
            }

            public int Decrement() {
                if (colliderCount > 0)
                    colliderCount--;
                return colliderCount;
            }

            public void SetTouch(bool value) {
                touchValid = value;
            }

            public bool Equals(ColliderData other) {
                return this.icollider == other.icollider;
            }

            public override int GetHashCode() {
                return icollider.GetHashCode();
            }
        }

        class ColliderTracker {
            public Dictionary<VRInteractorCollider, List<Collider>> colliderList = new Dictionary<VRInteractorCollider, List<Collider>>();
            public int Add(Collider collider) {
                VRInteractorCollider ic = collider.GetComponent<VRInteractorCollider>();
                if (ic == null)
                    return 0;
                if (!colliderList.ContainsKey(ic))
                    colliderList.Add(ic, new List<Collider>());
                colliderList[ic].Add(collider);
                return colliderList[ic].Count;
            }
        }
    }
}