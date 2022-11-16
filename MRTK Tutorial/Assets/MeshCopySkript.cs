using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//using Microsoft.MixedReality.Toolkit.SpatialAwareness;

public class MeshCopySkript : MonoBehaviour
{
    public bool permaMeshUpdate = false;
    public GameObject meshHolderPrefab;
    //public GameObject testCupPrefab;
    public GameObject[] trees;
    public LayerMask colLayer;
    public Material mat;
    public GameObject mixedRealityPlayspace;
    [HideInInspector]
    public GameObject[] meshCopyCollection;
    [HideInInspector]
    public MeshRenderer[] mcRenderer;
    [HideInInspector]
    public bool gotMesh;
    bool updatedOnce = false;

    Transform OpenSMO;
    Transform SAS;
    List<GameObject> placedObjects;
    float updateTimer;
    float floorHeight;
    public float treeLine;

    // bool startedToCopyMesh = false;

    // Start is called before the first frame update
    void Start()
    {
        placedObjects = new List<GameObject>();
        //Debug.Log(SAS.name);
        updateTimer = -8;
        floorHeight = float.MaxValue;
        treeLine = 0.3f;
    }

    // Update is called once per frame
    void Update()
    {
        updateTimer += Time.deltaTime;

        // Setup once
        if (!updatedOnce && updateTimer > 0)
        {
            mixedRealityPlayspace = GameObject.Find("MixedRealityPlayspace");
            SAS = mixedRealityPlayspace.transform.Find("Spatial Awareness System");
            OpenSMO = SAS.Find("OpenXR Spatial Mesh Observer");
        }
        //
        if (updateTimer > 0.5f && (permaMeshUpdate || !updatedOnce) )
        {
            StartCoroutine(updateMesh());
            updatedOnce = true;
            updateTimer = 0;
        }
    }

    private IEnumerator updateMesh()
    {


        foreach (GameObject mesh in meshCopyCollection)
        {
            Destroy(mesh);
        }
        meshCopyCollection = new GameObject[OpenSMO.childCount];
        for (int i = 0; i < OpenSMO.childCount; i++)
        {
            GameObject newMeshHolder = Instantiate(meshHolderPrefab, transform);
            MeshFilter meshFilter = newMeshHolder.GetComponent<MeshFilter>();
            Mesh newMesh = meshFilter.mesh;
            Mesh originalMesh = OpenSMO.GetChild(i).GetComponent<MeshFilter>().mesh;

            newMesh.SetVertices(originalMesh.vertices);
            newMesh.SetNormals(originalMesh.normals);
            newMesh.SetTriangles(originalMesh.triangles, 0);
            newMeshHolder.GetComponent<MeshCollider>().sharedMesh = newMesh;

            Vector3[] vertices = newMesh.vertices;
            Vector2[] uvs = new Vector2[vertices.Length];

            // Create UVs and find lowest point as the floor
            for (int v = 0; v < uvs.Length; v++)
            {
                uvs[v] = new Vector2(vertices[v].x, vertices[v].z);
                
                if (vertices[v].y < floorHeight)
                {
                    floorHeight = vertices[v].y;
                }
            }
            newMesh.SetUVs(0, uvs);
            meshCopyCollection[i] = newMeshHolder;
        }

        //clear all objects
        foreach (GameObject obj in placedObjects)
        {
            // Debug.Log("Destroying", obj);
            Destroy(obj);
        }
        placedObjects.RemoveAll(o => o == null);

        //testing collision:

        for (float x = -2; x <= 2; x += 0.25f)
        {
            for (float z = -2; z <= 2; z += 0.25f)
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(x, 0f, z), transform.TransformDirection(Vector3.down), out hit, 3, colLayer))
                {
                    //Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
                    // Only if flat at not at the ground
                    Vector3 hp = hit.transform.position;
                    Mathf.PerlinNoise(hp.x, hp.z);
                    if (hit.normal.y > 0.9f && hp.y > floorHeight + treeLine)
                    {
                        GameObject newObj = Instantiate(trees[Random.Range(0, trees.Length - 1)], hit.point, Quaternion.LookRotation(Vector3.forward, hit.normal));
                        placedObjects.Add(newObj);
                    }
                }
            }
        }




        //start Rock spawning
        Mesh[] meshesMesh = new Mesh[meshCopyCollection.Length];
        for (int i=0; i < meshCopyCollection.Length;i++)
        {
            meshesMesh[i] = meshCopyCollection[i].GetComponent<MeshFilter>().mesh;
        }
        GetComponent<SpawnRocks>().StartRockSpawning(meshesMesh);
        yield return null;
    }
}
