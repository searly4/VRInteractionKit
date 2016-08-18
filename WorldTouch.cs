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

namespace WorldUI {

    public class WorldTouch : MonoBehaviour, IWorldInteractor {

        public LayerMask layerMask;

        [HideInInspector]
        public Vector3 pointerHitPosition = Vector3.zero;

        bool nextState = false;


        public bool isInteracting {
            get;
            set;
        }

        public bool triggerPressed { get; protected set; }
        public bool triggerReleased { get; protected set; }
        public bool triggerDown { get; protected set; }

        public delegate void TouchEventHandler(WorldTouch source, Collider other, Vector3 position);
        public event TouchEventHandler Touched;
        // Use this for initialization
        void Start() {

        }

        void Update() {
            if (triggerDown != nextState) {
                triggerDown = nextState;
                triggerPressed = nextState;
                triggerReleased = !nextState;
                return;
            }
            triggerPressed = false;
            triggerReleased = false;
        }

        void OnEnable() {
        }

        void OnDisable() {
        }

        void OnCollisionEnter(Collision collision) {
            ContactPoint[] points = collision.contacts;
        }

        void OnCollisionExit(Collision collision) {
            ContactPoint[] points = collision.contacts;
            foreach (ContactPoint p in points) {
            }
        }

        void OnCollisionStay(Collision collision) {
            ContactPoint[] points = collision.contacts;

            TouchEventHandler eh = Touched;
            if (eh != null)
                eh(this, collision.collider, points[0].point);
        }

        void OnTriggerEnter(Collider collider) {
            //Debug.Log("Trigger entered.");
        }

        void OnTriggerExit(Collider collider) {
            //Debug.Log("Trigger exited.");
        }

        void OnTriggerStay(Collider collider) {
        }

        public void SetTriggerState(bool state) {
            nextState = state;
        }
    }
}
