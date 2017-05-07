using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class ElectromagneticFieldControllerScript : MonoBehaviour {
   
    public Vector3 ambientMagneticField;

    public float colliderPoof = 1.05f;
    public float dampenLanding = 0.5f;
    public int dampenLandingSteps = 10;
    public int collisionLayer = 9;
    
    public float maxMagnetizedForceOverMass = 20.0f;

    private List<Func<Vector3,Vector3> > magneticFieldFunctions = new List<Func<Vector3,Vector3> >();
    private List<Func<Vector3,Vector3> > magneticFieldDxFunctions = new List<Func<Vector3,Vector3> >();
    private List<Func<Vector3,Vector3> > magneticFieldDyFunctions = new List<Func<Vector3,Vector3> >();
    private List<Func<Vector3,Vector3> > magneticFieldDzFunctions = new List<Func<Vector3,Vector3> >();

    private List<Rigidbody> rigidbodies = new List<Rigidbody>();
    private int magneticDipoleCount = 0;
    private Dictionary<int,Dictionary<int,Vector3> > magneticDipolePosition = new Dictionary<int,Dictionary<int,Vector3> >();
    private Dictionary<int,Dictionary<int,Vector3> > magneticDipoleMoment = new Dictionary<int,Dictionary<int,Vector3> >();
    private Dictionary<int,bool> magneticDipoleTorqueOnly = new Dictionary<int,bool>();
    private Dictionary<int,GameObject> magneticDipoles = new Dictionary<int,GameObject>();
    
    private Dictionary<int,Dictionary<int,int> > collisionState = new Dictionary<int,Dictionary<int,int> >();

    private bool IsFinite(Vector3 v) {
        return (!float.IsNaN(v.x)  &&  !float.IsNaN(v.y)  &&  !float.IsNaN(v.z)  &&  v.x != Mathf.Infinity  &&  v.y != Mathf.Infinity  &&  v.z != Mathf.Infinity);
    }
    
    private bool IsFinite(float v) {
        return (!float.IsNaN(v)  &&  v != Mathf.Infinity);
    }

    
    public void RegisterMagneticField(Func<Vector3,Vector3> magneticFieldFunction) {
        magneticFieldFunctions.Add(magneticFieldFunction);
    }


    public void AssignMagneticDipoleId(GameObject dipole, Rigidbody parentRigidbody, Collider parentCollider, float poof, bool applyTorqueOnly, out int rigidbodyId, out int magneticDipoleId) {
        if (poof < 0.0f) { poof = colliderPoof; }

        if (rigidbodies.Contains(parentRigidbody)) {
            rigidbodyId = 0;
            while (rigidbodies[rigidbodyId] != parentRigidbody) { rigidbodyId++; }
        }
        else {
            rigidbodyId = rigidbodies.Count;
            rigidbodies.Add(parentRigidbody);
            AddRigidBody(dipole, parentRigidbody, parentCollider, rigidbodyId, poof);
        }
        magneticDipoleId = (magneticDipoleCount++);
        
        magneticDipoleTorqueOnly[magneticDipoleId] = applyTorqueOnly;
        magneticDipoles[magneticDipoleId] = dipole;
    }
    
    void AddRigidBody(GameObject obj, Rigidbody parentRigidbody, Collider parentCollider, int rigidbodyId, float poof) {
        magneticDipolePosition[rigidbodyId] = new Dictionary<int,Vector3>();
        magneticDipoleMoment[rigidbodyId] = new Dictionary<int,Vector3>();

        obj.layer = collisionLayer;
        if (parentCollider.gameObject.GetComponent<Collider>() is BoxCollider) {
            BoxCollider triggerCollider = obj.AddComponent<BoxCollider>();
            triggerCollider.center = obj.transform.InverseTransformPoint(obj.transform.parent.TransformPoint(((BoxCollider)(parentCollider)).center));
            triggerCollider.size = obj.transform.InverseTransformDirection(obj.transform.parent.TransformDirection(((BoxCollider)(parentCollider)).size)) * poof;
            triggerCollider.isTrigger = true;
        }
        else if (parentCollider is SphereCollider) {
            SphereCollider triggerCollider = obj.AddComponent<SphereCollider>();
            triggerCollider.center = obj.transform.InverseTransformPoint(obj.transform.parent.TransformPoint(((SphereCollider)(parentCollider)).center));
            Vector3 scaling = obj.transform.InverseTransformPoint(obj.transform.parent.TransformPoint(Vector3.one));
            triggerCollider.radius = ((SphereCollider)(parentCollider)).radius * Mathf.Max(scaling.x, scaling.y, scaling.z) * poof;
            triggerCollider.isTrigger = true;
        }
        else if (parentCollider is CapsuleCollider) {
            CapsuleCollider triggerCollider = obj.AddComponent<CapsuleCollider>();
            triggerCollider.center = obj.transform.InverseTransformPoint(obj.transform.parent.TransformPoint(((CapsuleCollider)(parentCollider)).center));
            triggerCollider.direction = ((CapsuleCollider)(parentCollider)).direction;

            Vector3 linear;
            Vector3 round1;
            Vector3 round2;
            if (triggerCollider.direction == 0) {
                linear = Vector3.right;
                round1 = Vector3.up;
                round2 = Vector3.forward;
            }
            else if (triggerCollider.direction == 1) {
                round1 = Vector3.right;
                linear = Vector3.up;
                round2 = Vector3.forward;
            }
            else {
                round1 = Vector3.right;
                round2 = Vector3.up;
                linear = Vector3.forward;
            }
            linear = obj.transform.InverseTransformDirection(obj.transform.parent.TransformDirection(linear));
            round1 = obj.transform.InverseTransformDirection(obj.transform.parent.TransformDirection(round1));
            round2 = obj.transform.InverseTransformDirection(obj.transform.parent.TransformDirection(round2));

            triggerCollider.height = ((CapsuleCollider)(parentCollider)).height * linear.magnitude * poof;
            triggerCollider.radius = ((CapsuleCollider)(parentCollider)).radius * Mathf.Max(round1.magnitude, round2.magnitude) * poof;
            triggerCollider.isTrigger = true;
        }
        else if (parentCollider is MeshCollider) {
            MeshCollider triggerCollider = obj.AddComponent<MeshCollider>();
            
            Mesh oldMesh = ((MeshCollider)(parentCollider)).sharedMesh;
            Vector3[] oldVertices = oldMesh.vertices;
            Vector2[] oldUV = oldMesh.uv;
            int[] oldTriangles = oldMesh.triangles;
                        
            Mesh mesh = new Mesh();
            Vector3[] newVertices = new Vector3[oldVertices.Length];
            Vector2[] newUV = new Vector2[oldUV.Length];
            int[] newTriangles = new int[oldTriangles.Length];
            
            for (int i = 0;  i < oldVertices.Length;  i++) {
                newVertices[i] = obj.transform.InverseTransformPoint(obj.transform.parent.TransformPoint(oldVertices[i]));
            }
            for (int i = 0;  i < oldUV.Length;  i++) {
                newUV[i] = oldUV[i];
            }
            for (int i = 0;  i < oldTriangles.Length;  i++) {
                newTriangles[i] = oldTriangles[i];
            }
            mesh.vertices = newVertices;
            mesh.uv = newUV;
            mesh.triangles = newTriangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
    
            // now "poof" it out like the other collider types
            float size = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            for (int i = 0;  i < newVertices.Length;  i++) {
                vertices[i] += normals[i] * (poof - 1.0f) * size;
            }
            mesh.vertices = vertices;
            triggerCollider.sharedMesh = mesh;
            
            triggerCollider.convex = ((MeshCollider)(parentCollider)).convex;
            triggerCollider.smoothSphereCollisions = ((MeshCollider)(parentCollider)).smoothSphereCollisions;
            triggerCollider.isTrigger = true;
        }
        else {
            throw new System.Exception("StaticCharges and MagneticDipoles can only be embedded in objects with a BoxCollider, SphereCollider, CapsuleCollider, or a MeshCollider.");
        }
        
        collisionState[rigidbodyId] = new Dictionary<int,int>();
        for (int j = 0;  j < rigidbodyId;  j++) {
            collisionState[rigidbodyId][j] = 0;
        }
    }
 
    public void UpdateMagneticDipole(int rigidbodyId, int magneticDipoleId, Vector3 position, Vector3 moment) {
        if (magneticDipolePosition.ContainsKey(rigidbodyId)) {
            magneticDipolePosition[rigidbodyId][magneticDipoleId] = position;
            magneticDipoleMoment[rigidbodyId][magneticDipoleId] = moment;
        }
    }
    
    public void UpdateCollisionState(int one, int two, int state) {
        if (one > two) {
            collisionState[one][two] = state;
            if (state == 0) {
                Joint.Destroy(rigidbodies[one].gameObject.GetComponent<Joint>());
            }
        }
        else {
            collisionState[two][one] = state;
            if (state == 0) {
                Joint.Destroy(rigidbodies[two].gameObject.GetComponent<Joint>());
            }
        }
    }
    
    public bool Colliding(int one, int two) {
        if (one > two) {
            return collisionState[one][two] > 0;
        }
        else {
            return collisionState[two][one] > 0;
        }
    }
    
    void FixedUpdate() {
        int rigidbodyId = 0;
        foreach (Rigidbody rigidbody in rigidbodies) {
            if (!rigidbody) {
                magneticDipolePosition.Remove(rigidbodyId);
                magneticDipoleMoment.Remove(rigidbodyId);
            }
            else {             
            
                if (magneticDipolePosition.ContainsKey(rigidbodyId)) {
                    foreach (int magneticDipoleId in magneticDipolePosition[rigidbodyId].Keys) {
                        Vector3 pos = magneticDipolePosition[rigidbodyId][magneticDipoleId];
                        Vector3 dip = magneticDipoleMoment[rigidbodyId][magneticDipoleId];
                        
                        if (!magneticDipoleTorqueOnly[magneticDipoleId]) {
                            Vector3 force = ForceOnMagneticDipole(rigidbodyId, pos, dip);
                            if (IsFinite(force)) {
                                rigidbody.AddForceAtPosition(force, pos, ForceMode.Impulse);
                            }
                        }
                        
                        Vector3 torque = TorqueOnMagneticDipole(rigidbodyId, pos, dip);
                        if (IsFinite(torque)) {
                            rigidbody.AddTorque(torque, ForceMode.Impulse);
                        }
                    }
                }
            }
            
            rigidbodyId++;
        }
        
        for (int i = 0;  i < rigidbodies.Count;  i++) {
            for (int j = 0;  j < i;  j++) {
                if (collisionState[i][j] > 0) {
                    if (collisionState[i][j] < dampenLandingSteps) {
                        rigidbodies[i].velocity *= dampenLanding;
                        rigidbodies[j].velocity *= dampenLanding;
                        collisionState[i][j] += 1;
                    }
                }
            }
        }
    }
    
    
    Vector3 ForceOnMagneticDipole(int rigidbodyId, Vector3 pos, Vector3 dipoleMoment) {
        Vector3 curlB;
        Vector3 ddxB;
        Vector3 ddyB;
        Vector3 ddzB;
        MagneticFieldDerivatives(pos, out curlB, out ddxB, out ddyB, out ddzB, rigidbodyId);
        return Vector3.Cross(dipoleMoment, curlB) + dipoleMoment.x*ddxB + dipoleMoment.y*ddyB + dipoleMoment.z*ddzB;
    }

    Vector3 TorqueOnMagneticDipole(int rigidbodyId, Vector3 pos, Vector3 dip) {
        return Vector3.Cross(dip, MagneticField(pos, rigidbodyId));
    }   
    
    public Vector3 MagneticField(Vector3 pos, int rigidbodyId = -1, int excludeSuperconductors = 0) {
        Vector3 magnetField = ambientMagneticField;

        foreach (Func<Vector3,Vector3> fieldFunction in magneticFieldFunctions) {
            magnetField += fieldFunction(pos);
        }

        foreach (int rId in magneticDipolePosition.Keys) {
            if (rId != rigidbodyId) {
                foreach (int mId in magneticDipolePosition[rId].Keys) {
                    Vector3 objectPos = magneticDipolePosition[rId][mId];
                    Vector3 dipoleMoment = magneticDipoleMoment[rId][mId];

                    Vector3 r = (pos - objectPos);
                    Vector3 rNorm = r.normalized;

                    magnetField += (3.0f * ((float)(Vector3.Dot(dipoleMoment, rNorm))) * rNorm - dipoleMoment) / Mathf.Pow(r.magnitude, 3) * 1e-7f;
                }
            }
        }
        
        return magnetField;
    }
    
    public void MagneticFieldDerivatives(Vector3 pos, out Vector3 curlField, out Vector3 dFieldx, out Vector3 dFieldy, out Vector3 dFieldz, int rigidbodyId = -1, int excludeSuperconductors = 0) {
        curlField = Vector3.zero;
        dFieldx = Vector3.zero;
        dFieldy = Vector3.zero;
        dFieldz = Vector3.zero;

        for (int i = 0;  i < magneticFieldDxFunctions.Count;  i++) {
            Vector3 dFieldxTemp = magneticFieldDxFunctions[i](pos);
            Vector3 dFieldyTemp = magneticFieldDyFunctions[i](pos);
            Vector3 dFieldzTemp = magneticFieldDzFunctions[i](pos);
            dFieldx += dFieldxTemp;
            dFieldy += dFieldyTemp;
            dFieldz += dFieldzTemp;
            curlField += new Vector3(dFieldyTemp.z - dFieldzTemp.y, dFieldzTemp.x - dFieldxTemp.z, dFieldxTemp.y - dFieldyTemp.x);
        }

        foreach (int rId in magneticDipolePosition.Keys) {
            if (rId != rigidbodyId  &&  (rigidbodyId < 0  ||  !Colliding(rId, rigidbodyId))) {
                foreach (int mId in magneticDipolePosition[rId].Keys) {
                    Vector3 objectPos = magneticDipolePosition[rId][mId];
                    Vector3 dipoleMoment = magneticDipoleMoment[rId][mId];
                    
                    Vector3 r = (pos - objectPos);
                    float mdotr = Vector3.Dot(dipoleMoment, r);
                    float overr2 = 1.0f / r.sqrMagnitude;
                    float overr5 = Mathf.Pow(r.magnitude, -5);
                    
                    Vector3 dFieldxTemp = 3.0f * (dipoleMoment.x*r + mdotr*Vector3.right + r.x*dipoleMoment - 5.0f*mdotr*r.x*overr2*r) * overr5;
                    Vector3 dFieldyTemp = 3.0f * (dipoleMoment.y*r + mdotr*Vector3.up + r.y*dipoleMoment - 5.0f*mdotr*r.y*overr2*r) * overr5;
                    Vector3 dFieldzTemp = 3.0f * (dipoleMoment.z*r + mdotr*Vector3.forward + r.z*dipoleMoment - 5.0f*mdotr*r.z*overr2*r) * overr5;

                    dFieldx += dFieldxTemp * 1e-7f;
                    dFieldy += dFieldyTemp * 1e-7f;
                    dFieldz += dFieldzTemp * 1e-7f;
                    curlField += new Vector3(dFieldyTemp.z - dFieldzTemp.y, dFieldzTemp.x - dFieldxTemp.z, dFieldxTemp.y - dFieldyTemp.x) * 1e-7f;
                }
            }
        }
        
    }
}
