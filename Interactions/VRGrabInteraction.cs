using UnityEngine;
using System.Collections;
using System;

namespace VRIK {
    public class VRGrabInteraction : VRInteraction {

        GameObject grabber = null;
        bool release = false;

        Rigidbody otherrb = null;
        Vector3 v;

        public VRInteractor grabbedInteractor { get; protected set; }

        public VRGrabInteraction() : base() {
            isPersistent = true;
        }

        // Use this for initialization
        new void Start() {
            base.Start();
        }

        void FixedUpdate() {
            if (grabber == null)
                return;

            ConfigurableJoint joint = grabber.GetComponent<ConfigurableJoint>();
            Rigidbody rb = grabber.GetComponent<Rigidbody>();
            Rigidbody otherrb = joint.connectedBody;
            Vector3 v = otherrb.GetPointVelocity(otherrb.worldCenterOfMass);
            if (release) {
                this.otherrb = otherrb;
                this.v = v;
                //v = transform.InverseTransformDirection(v) * 1000000;
                //v = new Vector3(1000, 1000, 1000);
                //otherrb.velocity = v;
                GameObject.DestroyImmediate(grabber.gameObject);
                grabber = null;
                release = false;
            }
            //if (this.otherrb != null)
                //this.otherrb.velocity = v;
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
            StopGrab(null);
            ReleaseTracker(tracker);
        }

        protected override void ReleaseTracker(InteractionInstanceTracker tracker) {
            base.ReleaseTracker(tracker);
        }

        public override bool CanInteract(VRInteractor otherInteractor) {
            if (otherInteractor.GetComponent<VRGrabInteraction>() != null)
                return false; // probably another controller

            return true;
        }

        public override InteractionInstanceTracker TryInteract(InteractionTracker tracker, VRInteractor otherInteractor) {
            InteractionTracker<bool> booltracker = tracker as InteractionTracker<bool>;
            if (!booltracker.value || !StartGrab(otherInteractor))
                return null;

            return RegisterTracker(tracker, otherInteractor);
        }

        bool StartGrab(VRInteractor otherInteractor) {
            if (grabber != null)
                return false;
            Rigidbody otherrb = otherInteractor.GetComponent<Rigidbody>();
            if (otherrb == null)
                return false;

            grabber = new GameObject("GrabberInteraction", typeof(Rigidbody), typeof(ConfigurableJoint));
            grabber.transform.SetParent(transform);
            grabber.transform.position = otherInteractor.transform.position;
            Rigidbody rb = grabber.GetComponent<Rigidbody>();
            ConfigurableJoint joint = grabber.GetComponent<ConfigurableJoint>();

            rb.isKinematic = true;
            rb.useGravity = false;

            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;
            //joint.angularXLimitSpring = new SoftJointLimitSpring { damper = 10, spring = 10 };
            //joint.angularYZLimitSpring = new SoftJointLimitSpring { damper = 10, spring = 10 };
            //joint.linearLimitSpring = new SoftJointLimitSpring { damper = 10, spring = 10 };
            //joint.lowAngularXLimit = new SoftJointLimit { };
            //joint.highAngularXLimit = new SoftJointLimit { };
            /*joint.xDrive = new JointDrive { maximumForce = 10, positionDamper = 10, positionSpring = 10 };
            joint.yDrive = new JointDrive { maximumForce = 10, positionDamper = 10, positionSpring = 10 };
            joint.zDrive = new JointDrive { maximumForce = 10, positionDamper = 10, positionSpring = 10 };
            joint.angularXDrive = new JointDrive { maximumForce = 10, positionDamper = 10, positionSpring = 10 };
            joint.angularYZDrive = new JointDrive { maximumForce = 10, positionDamper = 10, positionSpring = 10 };//*/

            joint.connectedBody = otherrb;

            grabbedInteractor = otherInteractor;

            return true;
        }

        void StopGrab(VRInteractor otherInteractor) {
            if (grabber == null)
                return;

            grabbedInteractor = null;
            release = true;
            /*ConfigurableJoint joint = grabber.GetComponent<ConfigurableJoint>();
            Rigidbody rb = grabber.GetComponent<Rigidbody>();
            Rigidbody otherrb = joint.connectedBody;
            Vector3 v = otherrb.velocity;
            if (v.sqrMagnitude == 0)
                throw new Exception("Not moving");
            joint.connectedBody = null;
            otherrb.velocity = v;//*/

            //GameObject.DestroyImmediate(grabber);
            //grabber = null;
        }
    }
}
