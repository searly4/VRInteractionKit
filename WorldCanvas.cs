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
using UnityEngine.EventSystems;

namespace WorldUI {

    public class WorldCanvas : MonoBehaviour {
        public bool triggerOnTouch = true;

        WorldInputModule wi = null;

        // Use this for initialization
        void Start() {
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider == null)
                collider = gameObject.AddComponent<BoxCollider>();
            RectTransform canvas = GetComponent<RectTransform>();
            Vector2 rectsize = canvas.rect.size;
            collider.size = new Vector3(rectsize.x, rectsize.y, 0);
            collider.isTrigger = true;

            wi = EventSystem.current.GetComponent<WorldInputModule>();
        }

        public void PointerHit(WorldTouch source, Vector3 p) {
            source.isInteracting = true;
            wi.Interactor = source;
            p = transform.transform.InverseTransformPoint(p);
            wi.VRPosition = new Vector2(p.x, p.y);
        }

        void PointerHit(WorldTouch source) {
            Vector3 p = transform.InverseTransformPoint(source.transform.position);
            p.z = 0;
            PointerHit(source, p);
        }

        void OnTriggerEnter(Collider collider) {
            Debug.Log("Trigger entered.");
            WorldTouch source = collider.GetComponent<WorldTouch>();
            if (source == null)
                return;
            PointerHit(source, source.transform.position);
        }

        void OnTriggerExit(Collider collider) {
            Debug.Log("Trigger exited.");
            WorldTouch source = collider.GetComponent<WorldTouch>();
            if (source == null)
                return;

            source.isInteracting = false;

            if (wi.Interactor == (IWorldInteractor)source)
                wi.Interactor = null;
        }

        void OnTriggerStay(Collider collider) {
            WorldTouch source = collider.GetComponent<WorldTouch>();
            if (source == null)
                return;
            PointerHit(source, source.transform.position);
        }

    }
}