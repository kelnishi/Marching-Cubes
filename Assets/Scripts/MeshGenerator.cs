﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using UnityEditor.UIElements;
using UnityEngine;
using Plane = UnityEngine.Plane;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

[ExecuteInEditMode]
public class MeshGenerator : MonoBehaviour {

    const int threadGroupSize = 8;

    [Header ("General Settings")]
    public DensityGenerator densityGenerator;

    public bool fixedMapSize;
    [ConditionalHide (nameof (fixedMapSize), true)]
    public Vector3Int numChunks = Vector3Int.one;
    [ConditionalHide (nameof (fixedMapSize), false)]
    public Transform viewer;
    [ConditionalHide (nameof (fixedMapSize), false)]
    public float viewDistance = 30;

    [Space ()]
    public bool autoUpdateInEditor = true;
    public bool autoUpdateInGame = true;

    public ComputeShader shader;
    public Material mat;
    public bool generateColliders;

    [Header ("Voxel Settings")]
    public float isoLevel;
    public float boundsSize = 1;
    public Vector3 offset = Vector3.zero;

    [Range (2, 256)]
    public int numPointsPerAxis = 30;

    [Header ("Gizmos")]
    public bool showBoundsGizmo = true;
    public Color boundsGizmoCol = Color.white;

    GameObject chunkHolder;
    const string chunkHolderName = "Chunks Holder";
    List<Chunk> chunks;
    Dictionary<Vector3Int, Chunk> existingChunks;
    Queue<Chunk> recycleableChunks;

    // Buffers
    // ComputeBuffer hashTable;
    // ComputeBuffer edgeBuffer;
    ComputeBuffer vertexBuffer;
    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer countBuffer;

    bool settingsUpdated;

    void Awake () {
        if (Application.isPlaying && !fixedMapSize) {
            InitVariableChunkStructures ();

            var oldChunks = FindObjectsOfType<Chunk> ();
            for (int i = oldChunks.Length - 1; i >= 0; i--) {
                Destroy (oldChunks[i].gameObject);
            }
        }
    }

    void Update () {
        // Update endless terrain
        if ((Application.isPlaying && !fixedMapSize)) {
            Run ();
        }

        if (settingsUpdated) {
            RequestMeshUpdate ();
            settingsUpdated = false;
        }
    }

    public void Run () {
        CreateBuffers ();

        if (fixedMapSize) {
            InitChunks ();
            UpdateAllChunks ();

        } else {
            if (Application.isPlaying) {
                InitVisibleChunks ();
            }
        }

        // Release buffers immediately in editor
        if (!Application.isPlaying) {
            ReleaseBuffers ();
        }

    }

    public void RequestMeshUpdate () {
        if ((Application.isPlaying && autoUpdateInGame) || (!Application.isPlaying && autoUpdateInEditor)) {
            Run ();
        }
    }

    void InitVariableChunkStructures () {
        recycleableChunks = new Queue<Chunk> ();
        chunks = new List<Chunk> ();
        existingChunks = new Dictionary<Vector3Int, Chunk> ();
    }

    void InitVisibleChunks () {
        if (chunks==null) {
            return;
        }
        CreateChunkHolder ();

        Vector3 p = viewer.position;
        Vector3 ps = p / boundsSize;
        Vector3Int viewerCoord = new Vector3Int (Mathf.RoundToInt (ps.x), Mathf.RoundToInt (ps.y), Mathf.RoundToInt (ps.z));

        int maxChunksInView = Mathf.CeilToInt (viewDistance / boundsSize);
        float sqrViewDistance = viewDistance * viewDistance;

        // Go through all existing chunks and flag for recyling if outside of max view dst
        for (int i = chunks.Count - 1; i >= 0; i--) {
            Chunk chunk = chunks[i];
            Vector3 centre = CentreFromCoord (chunk.coord);
            Vector3 viewerOffset = p - centre;
            Vector3 o = new Vector3 (Mathf.Abs (viewerOffset.x), Mathf.Abs (viewerOffset.y), Mathf.Abs (viewerOffset.z)) - Vector3.one * boundsSize / 2;
            float sqrDst = new Vector3 (Mathf.Max (o.x, 0), Mathf.Max (o.y, 0), Mathf.Max (o.z, 0)).sqrMagnitude;
            if (sqrDst > sqrViewDistance) {
                existingChunks.Remove (chunk.coord);
                recycleableChunks.Enqueue (chunk);
                chunks.RemoveAt (i);
            }
        }

        for (int x = -maxChunksInView; x <= maxChunksInView; x++) {
            for (int y = -maxChunksInView; y <= maxChunksInView; y++) {
                for (int z = -maxChunksInView; z <= maxChunksInView; z++) {
                    Vector3Int coord = new Vector3Int (x, y, z) + viewerCoord;

                    if (existingChunks.ContainsKey (coord)) {
                        continue;
                    }

                    Vector3 centre = CentreFromCoord (coord);
                    Vector3 viewerOffset = p - centre;
                    Vector3 o = new Vector3 (Mathf.Abs (viewerOffset.x), Mathf.Abs (viewerOffset.y), Mathf.Abs (viewerOffset.z)) - Vector3.one * boundsSize / 2;
                    float sqrDst = new Vector3 (Mathf.Max (o.x, 0), Mathf.Max (o.y, 0), Mathf.Max (o.z, 0)).sqrMagnitude;

                    // Chunk is within view distance and should be created (if it doesn't already exist)
                    if (sqrDst <= sqrViewDistance) {

                        Bounds bounds = new Bounds (CentreFromCoord (coord), Vector3.one * boundsSize);
                        if (IsVisibleFrom (bounds, Camera.main)) {
                            if (recycleableChunks.Count > 0) {
                                Chunk chunk = recycleableChunks.Dequeue ();
                                chunk.coord = coord;
                                existingChunks.Add (coord, chunk);
                                chunks.Add (chunk);
                                UpdateChunkMesh (chunk);
                            } else {
                                Chunk chunk = CreateChunk (coord);
                                chunk.coord = coord;
                                chunk.SetUp (mat, generateColliders);
                                existingChunks.Add (coord, chunk);
                                chunks.Add (chunk);
                                UpdateChunkMesh (chunk);
                            }
                        }
                    }

                }
            }
        }
    }

    public bool IsVisibleFrom (Bounds bounds, Camera camera) {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes (camera);
        return GeometryUtility.TestPlanesAABB (planes, bounds);
    }

    public Vector3Int CoordFromIndex(int index)
    {
        Vector3Int coord = new Vector3Int();
        coord.x = index % numPointsPerAxis;
        index = index / numPointsPerAxis;
        coord.y = index % numPointsPerAxis;
        index = index / numPointsPerAxis;
        coord.z = index;
        return coord;
    }
    
    public void edgeIdToCoords(int id, out Vector3Int coord, out Vector3Int dir)
    {
        coord = new Vector3Int();
        int xyz = id % 3;
        id = id / 3;
        coord.x = id % numPointsPerAxis;
        id = id / numPointsPerAxis;
        coord.y = id % numPointsPerAxis;
        id = id / numPointsPerAxis;
        coord.z = id;
        switch (xyz)
        {
            case 0: dir = Vector3Int.right; break;
            case 1: dir = Vector3Int.up; break;
            case 2: dir = Vector3Int.forward; break;
            default: dir = Vector3Int.zero; break;
        }
    }
    
    public string edgeFromId(int id)
    {
        Vector3Int c;
        Vector3Int dir;
        edgeIdToCoords(id, out c,out dir);

        string s =
            (dir == Vector3Int.forward) ? "Z" :
            (dir == Vector3Int.up) ? "Y" :
            "X";
        
        return $"{id} <{c.x},{c.y},{c.z}>{s}";
    }

    int indexFromCoord(Vector3Int c) {
        return c.z * numPointsPerAxis * numPointsPerAxis + c.y * numPointsPerAxis + c.x;
    }

    public void UpdateChunkMesh (Chunk chunk) {
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numThreadsPerAxis = Mathf.CeilToInt (numPointsPerAxis / (float) threadGroupSize);
        float pointSpacing = boundsSize / (numPointsPerAxis - 1);

        Vector3Int coord = chunk.coord;
        Vector3 centre = CentreFromCoord (coord);
        Vector3 worldBounds = new Vector3 (numChunks.x, numChunks.y, numChunks.z) * boundsSize;

        densityGenerator.Generate (pointsBuffer, numPointsPerAxis, boundsSize, worldBounds, centre, offset, pointSpacing);
        
        shader.SetBuffer(0, "verts", vertexBuffer);
        shader.SetBuffer (0, "points", pointsBuffer);
        shader.SetVector ("centre", new Vector4 (centre.x, centre.y, centre.z));
        shader.SetFloat ("spacing", pointSpacing);
        shader.SetFloat ("boundsSize", boundsSize);
        
        triangleBuffer.SetCounterValue (0);
        shader.SetBuffer (0, "triangles", triangleBuffer);
        shader.SetInt ("numPointsPerAxis", numPointsPerAxis);
        shader.SetFloat ("isoLevel", isoLevel);

        shader.Dispatch (0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
        
        int[] countArray = { 0 };
        
        Mesh mesh = chunk.mesh;
        mesh.Clear ();
        
        //Sparse matrix of edge verts
        //Ideally we could reuse this buffer raw without copying it back from the GPU.
        Vector3[] verts = new Vector3[numPoints*3];
        vertexBuffer.GetData(verts, 0, 0, numPoints*3);
        mesh.vertices = verts;
        
        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount (triangleBuffer, countBuffer, 0);
        countBuffer.GetData (countArray);
        int numTris = countArray[0];
        
        // Get triangle data from shader
        //Also would be nice if we could use this raw on the GPU without copying it back.
        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData (tris, 0, 0, numTris);
        
        //Do a fast memory unpack
        GCHandle handle = GCHandle.Alloc(tris, GCHandleType.Pinned);
        try
        {
            IntPtr pointer = handle.AddrOfPinnedObject();
            int[] indices = new int[numTris * 3];
            Marshal.Copy(pointer, indices, 0, indices.Length);
            
            mesh.triangles = indices;
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
        
        mesh.RecalculateNormals ();
    }

    public void UpdateAllChunks () {

        // Create mesh for each chunk
        foreach (Chunk chunk in chunks) {
            UpdateChunkMesh (chunk);
        }

    }

    void OnDestroy () {
        if (Application.isPlaying) {
            ReleaseBuffers ();
        }
    }

    
    struct Triangle 
    {
        //Vertex indices = Cube.index + x/y/z
        public int a;
        public int b;
        public int c;
    }
    
    void CreateBuffers () {
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;
        
        int maxEdgeCount = numPoints * 3; //Yes, this has hairs sticking out out 3 sides...

        // Always create buffers in editor (since buffers are released immediately to prevent memory leak)
        // Otherwise, only create if null or if size has changed
        if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
            if (Application.isPlaying) {
                ReleaseBuffers ();
            }
            
            
            vertexBuffer = new ComputeBuffer(maxEdgeCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
            triangleBuffer = new ComputeBuffer (maxTriangleCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Triangle)), ComputeBufferType.Append);
            pointsBuffer = new ComputeBuffer (numPoints, System.Runtime.InteropServices.Marshal.SizeOf(typeof(float)));
            countBuffer = new ComputeBuffer (1, sizeof (int), ComputeBufferType.Raw);

        }
    }

    void ReleaseBuffers () {
        if (triangleBuffer != null) {
            vertexBuffer.Release();
            triangleBuffer.Release ();
            pointsBuffer.Release ();
            countBuffer.Release ();
        }
    }
    
    static uint pcg_hash(uint input)
    {
        uint state = input * 747796405u + 2891336453u;
        uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
        return (word >> 22) ^ word;
    }

    Vector3 CentreFromCoord (Vector3Int coord) {
        // Centre entire map at origin
        if (fixedMapSize) {
            Vector3 totalBounds = (Vector3) numChunks * boundsSize;
            return -totalBounds / 2 + (Vector3) coord * boundsSize + Vector3.one * boundsSize / 2;
        }

        return new Vector3 (coord.x, coord.y, coord.z) * boundsSize;
    }

    void CreateChunkHolder () {
        // Create/find mesh holder object for organizing chunks under in the hierarchy
        if (chunkHolder == null) {
            if (GameObject.Find (chunkHolderName)) {
                chunkHolder = GameObject.Find (chunkHolderName);
            } else {
                chunkHolder = new GameObject (chunkHolderName);
            }
        }
    }

    // Create/get references to all chunks
    void InitChunks () {
        CreateChunkHolder ();
        chunks = new List<Chunk> ();
        List<Chunk> oldChunks = new List<Chunk> (FindObjectsOfType<Chunk> ());

        // Go through all coords and create a chunk there if one doesn't already exist
        for (int x = 0; x < numChunks.x; x++) {
            for (int y = 0; y < numChunks.y; y++) {
                for (int z = 0; z < numChunks.z; z++) {
                    Vector3Int coord = new Vector3Int (x, y, z);
                    bool chunkAlreadyExists = false;

                    // If chunk already exists, add it to the chunks list, and remove from the old list.
                    for (int i = 0; i < oldChunks.Count; i++) {
                        if (oldChunks[i].coord == coord) {
                            chunks.Add (oldChunks[i]);
                            oldChunks.RemoveAt (i);
                            chunkAlreadyExists = true;
                            break;
                        }
                    }

                    // Create new chunk
                    if (!chunkAlreadyExists) {
                        var newChunk = CreateChunk (coord);
                        chunks.Add (newChunk);
                    }

                    chunks[chunks.Count - 1].SetUp (mat, generateColliders);
                }
            }
        }

        // Delete all unused chunks
        for (int i = 0; i < oldChunks.Count; i++) {
            oldChunks[i].DestroyOrDisable ();
        }
    }

    Chunk CreateChunk (Vector3Int coord) {
        GameObject chunk = new GameObject ($"Chunk ({coord.x}, {coord.y}, {coord.z})");
        chunk.transform.parent = chunkHolder.transform;
        Chunk newChunk = chunk.AddComponent<Chunk> ();
        newChunk.coord = coord;
        return newChunk;
    }

    void OnValidate() {
        settingsUpdated = true;
    }
  

    void OnDrawGizmos () {
        if (showBoundsGizmo) {
            Gizmos.color = boundsGizmoCol;

            List<Chunk> chunks = (this.chunks == null) ? new List<Chunk> (FindObjectsOfType<Chunk> ()) : this.chunks;
            foreach (var chunk in chunks) {
                Bounds bounds = new Bounds (CentreFromCoord (chunk.coord), Vector3.one * boundsSize);
                Gizmos.color = boundsGizmoCol;
                Gizmos.DrawWireCube (CentreFromCoord (chunk.coord), Vector3.one * boundsSize);
            }
        }
    }

}