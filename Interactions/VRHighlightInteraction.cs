using UnityEngine;
using System.Collections.Generic;
using System;

namespace VRIK {
    public class VRHighlightInteraction : VRInteraction {
        public Color highlightColor = Color.yellow;
        public bool highlightChildren = false;

        List<ColorSet> originalObjectColours = null;
        List<VRInteractor> interactors = new List<VRInteractor>();

        public VRHighlightInteraction() : base() {
            isPersistent = true;
        }

        // Use this for initialization
        new void Start() {
            base.Start();
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
            StopHighlight(null);
            ReleaseTracker(tracker);
        }

        protected override void ReleaseTracker(InteractionInstanceTracker tracker) {
            base.ReleaseTracker(tracker);
        }

        public override InteractionInstanceTracker TryInteract(InteractionTracker tracker, VRInteractor otherInteractor) {
            InteractionTracker<bool> booltracker = tracker as InteractionTracker<bool>;
            if (!booltracker.value)
                return null;
            InteractionInstanceTracker t = RegisterTracker(tracker, otherInteractor);
            StartHighlight(otherInteractor);
            return t;
        }

        void StartHighlight(VRInteractor otherInteractor) {
            VRInteractor interactor = GetComponent<VRInteractor>();
            if (originalObjectColours == null)
                originalObjectColours = GetColors(interactor);

            if (otherInteractor != null)
                interactors.Add(otherInteractor);
        }

        void StopHighlight(VRInteractor otherInteractor) {
            VRInteractor interactor = GetComponent<VRInteractor>();

            if (otherInteractor != null && interactors.Contains(otherInteractor)) {
                interactors.Remove(otherInteractor);
            } else if (otherInteractor == null) {
                interactors.Clear();
            }

            if (originalObjectColours == null)
                return;

            if (interactors.Count == 0 || otherInteractor == null) {
                RestoreColors(originalObjectColours);
                originalObjectColours = null;
            }
        }

        List<ColorSet> GetColors(VRInteractor interactor) {
            List<ColorSet> originalColors = new List<ColorSet>();

            List<Renderer> renderers = new List<Renderer>();
            if (highlightChildren)
                renderers.AddRange(interactor.GetComponentsInChildren<Renderer>());
            else
                renderers.Add(interactor.GetComponent<Renderer>());

            foreach (Renderer r in renderers) {
                if (r.material == null)
                    continue;

                // store old colors and set new colors
                foreach (ColorMask c in Enum.GetValues(typeof(ColorMask))) {
                    string colorName = c.ToString();
                    if (!r.material.HasProperty(colorName))
                        continue;

                    originalColors.Add(new ColorSet() {
                        renderer = r,
                        property = c,
                        value = r.material.GetColor(colorName)
                    });
                    r.material.SetColor(colorName, highlightColor);
                }
            }

            return originalColors;
        }

        void RestoreColors(List<ColorSet> originalColors) {
            foreach (ColorSet s in originalColors) {
                Renderer r = s.renderer;
                if (r == null || r.material == null)
                    continue;
                r.material.SetColor(s.property.ToString(), s.value);
            }

            originalColors.Clear();
        }

        [Flags]
        enum ColorMask {
            _Color,
            _EmissionColor,
            _SpecColor,
            _ReflectColor
        }
        struct ColorSet {
            public Renderer renderer;
            public ColorMask property;
            public Color value;
        }
        
        /*private Color[] StoreOriginalColors() {
            Renderer[] rendererArray = GetRendererArray();
            int length = rendererArray.Length;
            Color[] colors = new Color[length];

            for (int i = 0; i < length; i++) {
                var renderer = rendererArray[i];
                if (renderer.material.HasProperty("_Color")) {
                    colors[i] = renderer.material.color;
                }
            }
            return colors;
        }

        private Color[] BuildHighlightColorArray(Color color) {
            Renderer[] rendererArray = GetRendererArray();
            int length = rendererArray.Length;
            Color[] colors = new Color[length];

            for (int i = 0; i < length; i++) {
                colors[i] = color;
            }
            return colors;
        }

        private void ChangeColor(Color[] colors) {
            Renderer[] rendererArray = GetRendererArray();
            int i = 0;
            foreach (Renderer renderer in rendererArray) {

                if (renderer.material.HasProperty("_Color")) {
                    renderer.material.color = colors[i];
                }
                i++;
            }
        }//*/
    }
}
