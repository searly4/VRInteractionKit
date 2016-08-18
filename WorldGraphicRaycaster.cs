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

/*
 * Note: Portions of this code may have been copied from the Unity UI subsystem
 * sources.
 */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace WorldUI {
    [AddComponentMenu("Event/World Graphic Raycaster")]
    [RequireComponent(typeof(WorldCanvas))]
    public class WorldGraphicRaycaster : BaseRaycaster {
        protected const int kNoEventMaskSet = -1;

        public override int sortOrderPriority {
            get {
                // We need to return the sorting order here as distance will all be 0 for overlay.
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return canvas.sortingOrder;

                return base.sortOrderPriority;
            }
        }

        public override int renderOrderPriority {
            get {
                // We need to return the sorting order here as distance will all be 0 for overlay.
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return canvas.renderOrder;

                return base.renderOrderPriority;
            }
        }

        [FormerlySerializedAs("ignoreReversedGraphics")]
        [SerializeField]
        private bool m_IgnoreReversedGraphics = true;

        [FormerlySerializedAs("blockingObjects")]
        [SerializeField]
        private BlockingObjects m_BlockingObjects = BlockingObjects.None;

        [SerializeField]
        protected LayerMask m_BlockingMask = kNoEventMaskSet;

        public bool ignoreReversedGraphics { get { return m_IgnoreReversedGraphics; } set { m_IgnoreReversedGraphics = value; } }
        public BlockingObjects blockingObjects { get { return m_BlockingObjects; } set { m_BlockingObjects = value; } }


        private Canvas m_Canvas;

        private Canvas canvas {
            get {
                if (m_Canvas != null)
                    return m_Canvas;

                m_Canvas = GetComponent<Canvas>();
                return m_Canvas;
            }
        }

        WorldInputModule vri = null;
        BoxCollider boxCollider;
        RectTransform canvasRectTransform;

        [NonSerialized]
        private List<Graphic> m_RaycastResults = new List<Graphic>();

        [NonSerialized]
        static readonly List<Graphic> s_SortedGraphics = new List<Graphic>();

        public override Camera eventCamera {
            get {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    || (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null))
                    return null;

                return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
        }


        protected WorldGraphicRaycaster() { }

        protected override void Start() {
            base.Start();

        }

        protected override void OnEnable() {
            base.OnEnable();

            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
                boxCollider = gameObject.AddComponent<BoxCollider>();

            vri = EventSystem.current.GetComponent<WorldInputModule>();

            canvasRectTransform = GetComponent<RectTransform>();
            Vector2 rectsize = canvasRectTransform.rect.size;
            boxCollider.size = new Vector3(rectsize.x, rectsize.y, 0);
            boxCollider.center = canvasRectTransform.rect.center;
        }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) {
            if (canvas == null)
                return;

            Camera eventCamera;

            if (eventData is VRPointerEventData) {
                eventCamera = null;
            } else {
                eventCamera = this.eventCamera;

                // Convert to view space
                Vector2 pos;
                if (eventCamera == null)
                    pos = new Vector2(eventData.position.x / Screen.width, eventData.position.y / Screen.height);
                else
                    pos = eventCamera.ScreenToViewportPoint(eventData.position);

                // If it's outside the camera's viewport, do nothing
                if (pos.x < 0f || pos.x > 1f || pos.y < 0f || pos.y > 1f)
                    return;
            }

            float hitDistance = float.MaxValue;

            Ray ray = new Ray();

            if (eventCamera != null)
                ray = eventCamera.ScreenPointToRay(eventData.position);

            if (eventCamera != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && blockingObjects != BlockingObjects.None) {
                float dist = 100.0f;

                if (eventCamera != null)
                    dist = eventCamera.farClipPlane - eventCamera.nearClipPlane;

                if (blockingObjects == BlockingObjects.ThreeD || blockingObjects == BlockingObjects.All) {
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, dist, m_BlockingMask)) {
                        hitDistance = hit.distance;
                    }
                }

                if (blockingObjects == BlockingObjects.TwoD || blockingObjects == BlockingObjects.All) {
                    RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, dist, m_BlockingMask);

                    if (hit.collider != null) {
                        hitDistance = hit.fraction * dist;
                    }
                }
            }

            m_RaycastResults.Clear();
            Raycast(canvas, eventCamera, eventData.position, m_RaycastResults);

            for (var index = 0; index < m_RaycastResults.Count; index++) {
                var go = m_RaycastResults[index].gameObject;
                bool appendGraphic = true;

                if (ignoreReversedGraphics) {
                    if (eventCamera == null) {
                        // If we dont have a camera we know that we should always be facing forward
                        var dir = go.transform.rotation * Vector3.forward;
                        appendGraphic = Vector3.Dot(Vector3.forward, dir) > 0;
                    } else {
                        // If we have a camera compare the direction against the cameras forward.
                        var cameraFoward = eventCamera.transform.rotation * Vector3.forward;
                        var dir = go.transform.rotation * Vector3.forward;
                        appendGraphic = Vector3.Dot(cameraFoward, dir) > 0;
                    }
                }

                if (appendGraphic) {
                    float distance = 0;

                    if (eventCamera == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        distance = 0;
                    else {
                        Transform trans = go.transform;
                        Vector3 transForward = trans.forward;
                        // http://geomalgorithms.com/a06-_intersect-2.html
                        distance = (Vector3.Dot(transForward, trans.position - ray.origin) / Vector3.Dot(transForward, ray.direction));

                        // Check to see if the go is behind the camera.
                        if (distance < 0)
                            continue;
                    }

                    if (distance >= hitDistance)
                        continue;

                    var castResult = new RaycastResult {
                        gameObject = go,
                        module = this,
                        distance = distance,
                        screenPosition = eventData.position,
                        index = resultAppendList.Count,
                        depth = m_RaycastResults[index].depth,
                        sortingLayer = canvas.sortingLayerID,
                        sortingOrder = canvas.sortingOrder
                    };
                    resultAppendList.Add(castResult);
                }
            }
        }

        /// <summary>
        /// Perform a raycast into the screen and collect all graphics underneath it.
        /// </summary>

        private static void Raycast(Canvas canvas, Camera eventCamera, Vector2 pointerPosition, List<Graphic> results) {
            // Debug.Log("ttt" + pointerPoision + ":::" + camera);
            // Necessary for the event system
            RectTransform canvasRectTransform = canvas.GetComponent<RectTransform>();

            string debugText = "";
            //Vector2 canvasPosition = pointerPosition;
            Vector2 screenPosition = pointerPosition;
            //if (eventCamera != null)
            //RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, pointerPosition, eventCamera, out canvasPosition);
            if (eventCamera == null) {
                eventCamera = Camera.main;
                screenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, canvas.transform.TransformPoint(pointerPosition));
            }

            var foundGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
            for (int i = 0; i < foundGraphics.Count; ++i) {
                Graphic graphic = foundGraphics[i];

                // -1 means it hasn't been processed by the canvas, which means it isn't actually drawn
                if (graphic.depth == -1 || !graphic.raycastTarget)
                    continue;

                /*Rect r = graphic.rectTransform.rect;
                Vector2 canvasRelativeOrigin = canvas.transform.InverseTransformPoint(graphic.rectTransform.position);
                Vector2 lowerLeft = new Vector2(r.xMin + canvasRelativeOrigin.x, r.yMin + canvasRelativeOrigin.y);
                Vector2 upperRight = new Vector2(r.xMax + canvasRelativeOrigin.x, r.yMax + canvasRelativeOrigin.y);
                if (debugText == "")
                    debugText = string.Format("({0}, {1}), ({2}, {3})", Time.time, lowerLeft.x, upperRight.x, lowerLeft.y, upperRight.y);
                /*if (lowerLeft.x < canvasPosition.x && upperRight.x > canvasPosition.x && lowerLeft.y < canvasPosition.y && upperRight.y > canvasPosition.y) {
                    //Debug.Log("Hit");
                } else
                    continue;//*/

                //if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, canvasPosition, null))
                //continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, screenPosition, eventCamera))
                    continue;

                //debugText = string.Format("* ({0}, {1}), ({2}, {3})", Time.time, lowerLeft.x, upperRight.x, lowerLeft.y, upperRight.y);
                //if (graphic.Raycast(pointerPosition, eventCamera))
                if (graphic.Raycast(screenPosition, eventCamera)) {
                    s_SortedGraphics.Add(graphic);
                }
            }

            //debugText += string.Format("\n{0:0.000}  ({1:0.0}, {2:0.0}) -> ({3:0.0}, {4:0.0})", Time.time, pointerPosition.x, pointerPosition.y, screenPosition.x, screenPosition.y);
            //DebugTool.current.SetText(0, debugText);

            s_SortedGraphics.Sort((g1, g2) => g2.depth.CompareTo(g1.depth));
            //		StringBuilder cast = new StringBuilder();
            for (int i = 0; i < s_SortedGraphics.Count; ++i)
                results.Add(s_SortedGraphics[i]);
            //		Debug.Log (cast.ToString());

            s_SortedGraphics.Clear();
        }


        public void PointerHit(WorldTouch source, Vector3 p3) {
            vri.Interactor = source;
            p3 = transform.transform.InverseTransformPoint(p3);
            Vector2 p2 = new Vector2(p3.x, p3.y);
            //Vector2 p2 = new Vector2(p3.x * canvasRectTransform.localScale.x, p3.y * canvasRectTransform.localScale.y);
            //p2 -= canvasRectTransform.rect.position;
            vri.VRPosition = p2;
        }

        public enum BlockingObjects {
            None = 0,
            TwoD = 1,
            ThreeD = 2,
            All = 3,
        }
    }
}
