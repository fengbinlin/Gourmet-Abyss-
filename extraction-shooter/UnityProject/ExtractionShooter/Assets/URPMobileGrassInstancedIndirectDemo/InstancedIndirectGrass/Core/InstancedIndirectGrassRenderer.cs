//see this for ref: https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public class InstancedIndirectGrassRenderer : MonoBehaviour
{
    [Header("Grass Model")]
    public Mesh grassMeshAsset; // 在 Inspector 里指定一个模型
    public bool recalcNormals = true; // 是否重新计算法线
    public Vector3 forceNormalDirection = Vector3.zero; // 如果不为 Vector3.zero，就强制法线方向（如 Vector3.up）

    [Header("Settings")]
    public float drawDistance = 125;//this setting will affect performance a lot!
    public Material instanceMaterial;

    [Header("Internal")]
    public ComputeShader cullingComputeShader;

    [NonSerialized]
    public List<Vector3> allGrassPos = new List<Vector3>();//user should update this list using C#
    //=====================================================
    [HideInInspector]
    public static InstancedIndirectGrassRenderer instance;// global ref to this script

    private int cellCountX = -1;
    private int cellCountZ = -1;
    private int dispatchCount = -1;

    //smaller the number, CPU needs more time, but GPU is faster
    private float cellSizeX = 10; //unity unit (m)
    private float cellSizeZ = 10; //unity unit (m)

    private int instanceCountCache = -2;
    private Mesh cachedGrassMesh;

    private ComputeBuffer allInstancesPosWSBuffer;
    private ComputeBuffer visibleInstancesOnlyPosWSIDBuffer;
    private ComputeBuffer argsBuffer;

    private List<Vector3>[] cellPosWSsList; //for binning: binning will put each posWS into correct cell
    private float minX, minZ, maxX, maxZ;
    private List<int> visibleCellIDList = new List<int>();
    private Plane[] cameraFrustumPlanes = new Plane[6];

    bool shouldBatchDispatch = true;
    //=====================================================

    private void Awake()
    {
        instance = this; // assign global ref using this script
    }

    void LateUpdate()
    {
        if (allGrassPos == null || allGrassPos.Count == 0)
            return; // 没有草位置就不渲染
        // recreate all buffers if needed
        UpdateAllInstanceTransformBufferIfNeeded();

        //=====================================================================================================
        // rough quick big cell frustum culling in CPU first
        //=====================================================================================================
        visibleCellIDList.Clear();//fill in this cell ID list using CPU frustum culling first
        Camera cam = Camera.main;

        //Do frustum culling using per cell bound
        //https://docs.unity3d.com/ScriptReference/GeometryUtility.CalculateFrustumPlanes.html
        //https://docs.unity3d.com/ScriptReference/GeometryUtility.TestPlanesAABB.html
        float cameraOriginalFarPlane = cam.farClipPlane;
        cam.farClipPlane = drawDistance;//allow drawDistance control    
        GeometryUtility.CalculateFrustumPlanes(cam, cameraFrustumPlanes);//Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
        cam.farClipPlane = cameraOriginalFarPlane;//revert far plane edit

        //slow loop
        //TODO: (A)replace this forloop by a quadtree test?
        //TODO: (B)convert this forloop to job+burst? (UnityException: TestPlanesAABB can only be called from the main thread.)
        Profiler.BeginSample("CPU cell frustum culling (heavy)");

        for (int i = 0; i < cellPosWSsList.Length; i++)
        {
            //create cell bound
            Vector3 centerPosWS = new Vector3(i % cellCountX + 0.5f, 0, i / cellCountX + 0.5f);
            centerPosWS.x = Mathf.Lerp(minX, maxX, centerPosWS.x / cellCountX);
            centerPosWS.z = Mathf.Lerp(minZ, maxZ, centerPosWS.z / cellCountZ);
            //Camera Offerset
            int cameraXDiv = (int)((centerPosWS.z - Camera.main.transform.position.z) / 125);
            centerPosWS.z -= math.ceil(cameraXDiv*0.5f) * 250;

            Vector3 sizeWS = new Vector3(Mathf.Abs(maxX - minX) / cellCountX, 0, Mathf.Abs(maxX - minX) / cellCountX);
            Bounds cellBound = new Bounds(centerPosWS, sizeWS);
            //visibleCellIDList.Add(i);
            if (GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, cellBound))
            {
                visibleCellIDList.Add(i);
            }
        }
        Profiler.EndSample();

        //=====================================================================================================
        // then loop though only visible cells, each visible cell dispatch GPU culling job once
        // at the end compute shader will fill all visible instance into visibleInstancesOnlyPosWSIDBuffer
        //=====================================================================================================
        Matrix4x4 v = cam.worldToCameraMatrix;
        Matrix4x4 p = cam.projectionMatrix;
        Matrix4x4 vp = p * v;

        visibleInstancesOnlyPosWSIDBuffer.SetCounterValue(0);

        //set once only
        cullingComputeShader.SetMatrix("_VPMatrix", vp);
        cullingComputeShader.SetFloat("_MaxDrawDistance", drawDistance);
        // 新增：传递相机位置到Compute Shader
        cullingComputeShader.SetVector("_CameraPosWS", Camera.main.transform.position);  // 这行是新增的
        //dispatch per visible cell
        dispatchCount = 0;
        for (int i = 0; i < visibleCellIDList.Count; i++)
        {
            int targetCellFlattenID = visibleCellIDList[i];
            int memoryOffset = 0;
            for (int j = 0; j < targetCellFlattenID; j++)
            {
                memoryOffset += cellPosWSsList[j].Count;
            }
            cullingComputeShader.SetInt("_StartOffset", memoryOffset); //culling read data started at offseted pos, will start from cell's total offset in memory
            int jobLength = cellPosWSsList[targetCellFlattenID].Count;

            //============================================================================================
            //batch n dispatchs into 1 dispatch, if memory is continuous in allInstancesPosWSBuffer
            if (shouldBatchDispatch)
            {
                while ((i < visibleCellIDList.Count - 1) && //test this first to avoid out of bound access to visibleCellIDList
                        (visibleCellIDList[i + 1] == visibleCellIDList[i] + 1))
                {
                    //if memory is continuous, append them together into the same dispatch call
                    jobLength += cellPosWSsList[visibleCellIDList[i + 1]].Count;
                    i++;
                }
            }
            //============================================================================================

            cullingComputeShader.Dispatch(0, Mathf.CeilToInt(jobLength / 64f), 1, 1); //disaptch.X division number must match numthreads.x in compute shader (e.g. 64)
            dispatchCount++;
        }

        //====================================================================================
        // Final 1 big DrawMeshInstancedIndirect draw call 
        //====================================================================================
        // GPU per instance culling finished, copy visible count to argsBuffer, to setup DrawMeshInstancedIndirect's draw amount 
        ComputeBuffer.CopyCount(visibleInstancesOnlyPosWSIDBuffer, argsBuffer, 4);

        // Render 1 big drawcall using DrawMeshInstancedIndirect    
        //Bounds renderBound = new Bounds();

        //renderBound.SetMinMax(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, maxZ));//if camera frustum is not overlapping this bound, DrawMeshInstancedIndirect will not even render
        // 使用相机位置为中心，大小为1的Bounds（确保不会被剔除）
        Bounds renderBound = new Bounds(Camera.main.transform.position, Vector3.one);
        Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(), 0, instanceMaterial, renderBound, argsBuffer);

    }

    private void OnGUI()
    {
        GUI.contentColor = Color.black;
        GUI.Label(new Rect(200, 0, 400, 60),
            $"After CPU cell frustum culling,\n" +
            $"-Visible cell count = {visibleCellIDList.Count}/{cellCountX * cellCountZ}\n" +
            $"-Real compute dispatch count = {dispatchCount} (saved by batching = {visibleCellIDList.Count - dispatchCount})");

        shouldBatchDispatch = GUI.Toggle(new Rect(400, 400, 200, 100), shouldBatchDispatch, "shouldBatchDispatch");
    }

    void OnDisable()
    {
        //release all compute buffers
        if (allInstancesPosWSBuffer != null)
            allInstancesPosWSBuffer.Release();
        allInstancesPosWSBuffer = null;

        if (visibleInstancesOnlyPosWSIDBuffer != null)
            visibleInstancesOnlyPosWSIDBuffer.Release();
        visibleInstancesOnlyPosWSIDBuffer = null;

        if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = null;

        instance = null;
    }

    Mesh GetGrassMeshCache()
    {
        if (cachedGrassMesh == null)
        {
            if (grassMeshAsset != null)
            {
                // 复制一份（避免直接改原资源文件）
                cachedGrassMesh = Instantiate(grassMeshAsset);

                // 法线修正
                if (recalcNormals)
                {
                    cachedGrassMesh.RecalculateNormals();
                }

                // 如果强制法线方向
                if (forceNormalDirection != Vector3.zero)
                {
                    Vector3[] normals = cachedGrassMesh.normals;
                    for (int i = 0; i < normals.Length; i++)
                    {
                        normals[i] = forceNormalDirection.normalized;
                    }
                    cachedGrassMesh.normals = normals;
                }

                // 如果需要切线
                //cachedGrassMesh.RecalculateTangents();
            }
            else
            {
                Debug.LogWarning("No grass mesh assigned. Using default.");
                cachedGrassMesh = CreateSimpleTriangleGrass(); // 或 CreateSimpleTriangleGrass();
            }
        }
        return cachedGrassMesh;
    }

    Mesh CreateSimpleTriangleGrass()
    {
        Mesh m = new Mesh();
        Vector3[] verts = new Vector3[3] {
        new Vector3(-0.25f, 0),
        new Vector3(+0.25f, 0),
        new Vector3(0, 1)
    };
        int[] triangles = { 2, 1, 0 };
        m.SetVertices(verts);
        m.SetTriangles(triangles, 0);
        return m;
    }

    public void UpdateAllInstanceTransformBufferIfNeeded()
    {
        if (allGrassPos == null || allGrassPos.Count == 0)
        {
            return; // 没有数据直接退出，防止 min/max 计算错误
        }
        //always update
        instanceMaterial.SetVector("_PivotPosWS", transform.position);
        instanceMaterial.SetVector("_BoundSize", new Vector2(transform.localScale.x, transform.localScale.z));

        //early exit if no need to update buffer
        if (instanceCountCache == allGrassPos.Count &&
            argsBuffer != null &&
            allInstancesPosWSBuffer != null &&
            visibleInstancesOnlyPosWSIDBuffer != null)
        {
            return;
        }
        //print(allGrassPos.Count);
        //print(instanceCountCache);
        /////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        Debug.Log("UpdateAllInstanceTransformBuffer (Slow)");

        ///////////////////////////
        // allInstancesPosWSBuffer buffer
        ///////////////////////////
        if (allInstancesPosWSBuffer != null)
            allInstancesPosWSBuffer.Release();
        allInstancesPosWSBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(float) * 3); //float3 posWS only, per grass

        if (visibleInstancesOnlyPosWSIDBuffer != null)
            visibleInstancesOnlyPosWSIDBuffer.Release();
        visibleInstancesOnlyPosWSIDBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(uint), ComputeBufferType.Append); //uint only, per visible grass

        //find all instances's posWS XZ bound min max
        minX = float.MaxValue;
        minZ = float.MaxValue;
        maxX = float.MinValue;
        maxZ = float.MinValue;
        for (int i = 0; i < allGrassPos.Count; i++)
        {
            Vector3 target = allGrassPos[i];
            minX = Mathf.Min(target.x, minX);
            minZ = Mathf.Min(target.z, minZ);
            maxX = Mathf.Max(target.x, maxX);
            maxZ = Mathf.Max(target.z, maxZ);
        }

        //decide cellCountX,Z here using min max
        //each cell is cellSizeX x cellSizeZ
        cellCountX = Mathf.CeilToInt((maxX - minX) / cellSizeX);
        cellCountZ = Mathf.CeilToInt((maxZ - minZ) / cellSizeZ);

        //init per cell posWS list memory
        cellPosWSsList = new List<Vector3>[cellCountX * cellCountZ]; //flatten 2D array
        for (int i = 0; i < cellPosWSsList.Length; i++)
        {
            cellPosWSsList[i] = new List<Vector3>();
        }

        //binning, put each posWS into the correct cell
        for (int i = 0; i < allGrassPos.Count; i++)
        {
            Vector3 pos = allGrassPos[i];

            //find cellID
            int xID = Mathf.Min(cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(minX, maxX, pos.x) * cellCountX)); //use min to force within 0~[cellCountX-1]  
            int zID = Mathf.Min(cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(minZ, maxZ, pos.z) * cellCountZ)); //use min to force within 0~[cellCountZ-1]

            cellPosWSsList[xID + zID * cellCountX].Add(pos);
        }

        //combine to a flatten array for compute buffer
        int offset = 0;
        Vector3[] allGrassPosWSSortedByCell = new Vector3[allGrassPos.Count];
        for (int i = 0; i < cellPosWSsList.Length; i++)
        {
            for (int j = 0; j < cellPosWSsList[i].Count; j++)
            {
                allGrassPosWSSortedByCell[offset] = cellPosWSsList[i][j];
                offset++;
            }
        }
        //print(allGrassPosWSSortedByCell[0]);
        //print(allGrassPosWSSortedByCell[1]);
        //print(allGrassPosWSSortedByCell[2]);
        //print(allGrassPosWSSortedByCell[3]);
        allInstancesPosWSBuffer.SetData(allGrassPosWSSortedByCell);
        instanceMaterial.SetBuffer("_AllInstancesTransformBuffer", allInstancesPosWSBuffer);
        instanceMaterial.SetBuffer("_VisibleInstanceOnlyTransformIDBuffer", visibleInstancesOnlyPosWSIDBuffer);

        ///////////////////////////
        // Indirect args buffer
        ///////////////////////////
        if (argsBuffer != null)
            argsBuffer.Release();
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint)GetGrassMeshCache().GetIndexCount(0);
        args[1] = (uint)allGrassPos.Count;
        args[2] = (uint)GetGrassMeshCache().GetIndexStart(0);
        args[3] = (uint)GetGrassMeshCache().GetBaseVertex(0);
        args[4] = 0;

        argsBuffer.SetData(args);

        ///////////////////////////
        // Update Cache
        ///////////////////////////
        //update cache to prevent future no-op buffer update, which waste performance
        instanceCountCache = allGrassPos.Count;


        //set buffer
        cullingComputeShader.SetBuffer(0, "_AllInstancesPosWSBuffer", allInstancesPosWSBuffer);
        cullingComputeShader.SetBuffer(0, "_VisibleInstancesOnlyPosWSIDBuffer", visibleInstancesOnlyPosWSIDBuffer);
    }
}