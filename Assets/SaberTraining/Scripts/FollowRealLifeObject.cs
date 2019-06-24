﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForceBasedMove;


public class FollowRealLifeObject : MonoBehaviour
{
    public GameObject realLifeObject;// RealLifeVR layer
    public GameObject physicsVRObject;// GameVR layer

    public Transform realLifeCollisionSpotTransform;
    public Transform physicsVRCollisionSpotTransform;

    public List<MeshRenderer> realLifeGhostMeshes = new List<MeshRenderer>();

    public Rigidbody rb;
    public bool isCollisioning = false;

    public float forceCatchupScale = 14f;
    public float torqueCatchupScale = 14f;

    public float maxDistance = 0.5f;
    public OVRInput.Controller controllerKind = OVRInput.Controller.None;

    public OVRGrabbable grabbable;
    public bool isGrabbed = false;

    bool meshDisabled = false;

    public bool isJoiningBack = true;
    float joiningBackEndTime = -1;
    float joiningBackDuration = 0.2f;

    Vector3 grabSpotLocalPosition;
    Quaternion grabSpotLocalRotation;

    public Vector3 PhysicsVrGrabSpotPosition {
        get {
            return physicsVRObject.transform.position + grabSpotLocalPosition;
        }
    }

    public Vector3 RealLifeGrabSpotPosition
    {
        get
        {
            return realLifeObject.transform.position + grabSpotLocalPosition;
        }
    }

    public Quaternion PhysicsVrGrabSpotRotation {
        get {
            return physicsVRObject.transform.rotation * grabSpotLocalRotation;
        }
    }

    public Quaternion RealLifeGrabSpotRotation {
        get {
            return realLifeObject.transform.rotation * grabSpotLocalRotation;
        }
    }

    //TODO Handle hand_right_renderPart_0 Skinmeshrenderer material

    void Awake()
    {
        if(rb == null) rb = GetComponentInChildren<Rigidbody>();
        if (grabbable == null) grabbable = realLifeObject.GetComponent<OVRGrabbable>();
    }

    // Update is called once per frame
    void Update()
    {
        isGrabbed = grabbable.isGrabbed;
        if (isGrabbed && controllerKind == OVRInput.Controller.None)
        {
            controllerKind = OVRInput.Controller.None;
            if (grabbable.grabbedBy.name.Contains("Left")){
                controllerKind = OVRInput.Controller.LTouch;
                PhysicsVRHand.Left.grabbedObject = this;
            }
            if (grabbable.grabbedBy.name.Contains("Right")) {
                controllerKind = OVRInput.Controller.RTouch;
                PhysicsVRHand.Right.grabbedObject = this;
            }
            if (controllerKind != OVRInput.Controller.None)
            {
                grabSpotLocalPosition = grabbable.grabbedBy.transform.position - realLifeObject.transform.position;
                grabSpotLocalRotation = Quaternion.Inverse(realLifeObject.transform.rotation) * grabbable.grabbedBy.transform.rotation;
            }
        } else if (!isGrabbed && controllerKind != OVRInput.Controller.None) {
            controllerKind = OVRInput.Controller.None;
            OVRInput.SetControllerVibration(0, 0, controllerKind);
        }

        if (Time.time > joiningBackEndTime) isJoiningBack = false;
    }

    void EnableRealLifeMGhostMesh() {
        if (!meshDisabled) return;
        meshDisabled = false;
        foreach (var mesh in realLifeGhostMeshes) {
            mesh.enabled = true;
        }
    }

    void DisableRealLifeGhostMesh()
    {
        if (meshDisabled) return;
        meshDisabled = true;
        foreach (var mesh in realLifeGhostMeshes)
        {
            mesh.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if ((isCollisioning && isGrabbed)) {
            EnableRealLifeMGhostMesh();
            // Try to go back to real life object
            var forceCatchupScale = this.forceCatchupScale;
            var torqueCatchupScale = this.torqueCatchupScale;
            if (isJoiningBack)
            {
                forceCatchupScale *= 4;
                torqueCatchupScale *= 4;
            }

            rb.AddForceTowards(PhysicsVrGrabSpotPosition, RealLifeGrabSpotPosition, frequency: forceCatchupScale);
            rb.AddTorqueTowards(PhysicsVrGrabSpotRotation, RealLifeGrabSpotRotation, frequency: torqueCatchupScale);

            // Small additional force if the hand is in the right position, but bended
            rb.AddForceTowards(physicsVRCollisionSpotTransform.position, realLifeCollisionSpotTransform.position, frequency: forceCatchupScale/2);

            if (isGrabbed)
            {
                float distance = Vector3.Distance(physicsVRCollisionSpotTransform.position, realLifeCollisionSpotTransform.position);
                float amplitude = Mathf.Clamp(distance / maxDistance, 0, 1);
                OVRInput.SetControllerVibration(frequency: amplitude, amplitude: amplitude, controllerKind);
            } else
            {
                OVRInput.SetControllerVibration(0, 0.1f, controllerKind);
            }
        }
        else if (isJoiningBack)
        {
            var returnStart = joiningBackEndTime - joiningBackDuration;
            var returnProgress = (Time.time - returnStart) / joiningBackDuration;

            physicsVRObject.transform.position = Vector3.Lerp(physicsVRObject.transform.position, realLifeObject.transform.position, returnProgress);
            physicsVRObject.transform.rotation = Quaternion.Lerp(physicsVRObject.transform.rotation, realLifeObject.transform.rotation, returnProgress);
            physicsVRObject.GetComponent<Rigidbody>().velocity = Vector3.zero;
            physicsVRObject.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

            if (isGrabbed)
            {
                OVRInput.SetControllerVibration(0, 0, controllerKind);
            }
        }
        else
        {
            DisableRealLifeGhostMesh();

            physicsVRObject.transform.position = realLifeObject.transform.position;
            physicsVRObject.transform.rotation = realLifeObject.transform.rotation;

            if (isGrabbed)
            {
                OVRInput.SetControllerVibration(0, 0, controllerKind);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isJoiningBack) return;
        isCollisioning = true;
        Vector3 localPosition = physicsVRObject.transform.InverseTransformPoint(collision.GetContact(0).point);
        physicsVRCollisionSpotTransform.localPosition = localPosition;
        realLifeCollisionSpotTransform.localPosition = localPosition;
    }

    private void OnCollisionStay(Collision collision)
    {
        isCollisioning = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        isCollisioning = false;
        joiningBackEndTime = Time.time + joiningBackDuration;
        isJoiningBack = true;
    }
}
