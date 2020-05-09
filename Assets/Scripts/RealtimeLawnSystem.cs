using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.WSA;

public class RealtimeLawnSystem : MonoBehaviour {
    private Text fpsText;
    private Text countText;
    private float deltaTime;

    public ComputeShader mathShader;
    public Texture2D shadowTex;
    //all
    private ComputeBuffer sizeBuffer;
    private ComputeBuffer renderPosAppendBuffer;
    //frustum
    public ComputeShader calcShader;
    //grass
    public Texture2D grassDensityMap;
    public int grassAmountPerTile = 256;//实际渲染时每个Tile内最多的草叶数量
    public int minGrassPerTile = 0;
    public int pregenerateGrassAmount = 1023;//预生成Patch草体总长度
    public Material grassMaterial;
    public int bladeSectionCountMax;
    public int bladeSectionCountMin;
    //terrain
    public Texture2D heightMap;
    public float terrainHeight = 5f;
    public const int PATCH_SIZE = 2;//Patch的边长
    public Material terrainMat;

    private GrassGenerator grassGen;
    private TerrainBuilder terrainBuilder;
    private FrustumCalculation frustumCalc;
    private Mesh grassMesh;//for test应该改为private

    //indirect
    private ComputeBuffer argsBuffer;
    private Bounds instanceBound;
    private bool mShowDrawCount = false;
    private uint mDrawCount;

    void Awake() {
        Debug.Log(" SystemInfo.supportsComputeShaders: " + SystemInfo.supportsComputeShaders); 
        //GenMathData();
        sizeBuffer = new ComputeBuffer(6, sizeof(float));
        float[] sizeBufferData = new float[6];
        sizeBuffer.SetData(sizeBufferData);
        Shader.SetGlobalBuffer("sizeBuffer", sizeBuffer);
        //地形
        terrainBuilder = new TerrainBuilder(heightMap, terrainHeight, terrainMat);
        GameObject terrain = GameObject.Find("terrain");
        if (!terrain)
            terrainBuilder.BuildTerrain(transform);
        //视锥体
        frustumCalc = new FrustumCalculation(calcShader, terrainBuilder);
        //草叶
        grassGen = new GrassGenerator(grassDensityMap, grassAmountPerTile,
            pregenerateGrassAmount, grassMaterial, TerrainBuilder.PATCH_SIZE);
        grassMesh = grassGen.generateGrassTile();
        grassGen.PregenerateGrassInfo();

        //terrain data buffer
        Texture terrainTex = terrainBuilder.GetTerrainHeightTexture();
        Texture densityTex = grassGen.GetTerrainDensityTexture();
        Shader.SetGlobalTexture("terrainHeightTex", terrainTex);
        frustumCalc.SetTextureFromGlobal("terrainHeightTex");
        grassMaterial.SetTexture("terrainDensityTex", densityTex);

        Shader.SetGlobalFloat("terrainHeight", terrainHeight);
        frustumCalc.SetFloat("terrainHeight", terrainHeight);

        grassMaterial.SetInt("grassAmountPerTile", grassAmountPerTile);
        grassMaterial.SetInt("pregenerateGrassAmount", pregenerateGrassAmount);

        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5,
            ComputeBufferType.IndirectArguments);

        SetShadowTexture();
        
    }

    private void Start() {
        fpsText = GameObject.Find("fpsText").GetComponent<Text>();
        countText = GameObject.Find("countText").GetComponent<Text>();
        Application.targetFrameRate = -1;

        Button mCountButton = GameObject.Find("CountButton").GetComponent<Button>();
        mCountButton.onClick.AddListener(() => { mShowDrawCount = true; });
    }

void Update() {
        //视锥体计算
        Vector4 frustumSize;
        instanceBound = frustumCalc.PrepareCamData(Camera.main, out frustumSize);

        //更新sizeBuffer:场景大小、检测面积大小、检测起始点
        float[] sizeBufferData = { heightMap.width,heightMap.height,
        frustumSize.x,frustumSize.y,frustumSize.z,frustumSize.w};
        /*if (sizeBuffer != null)
            sizeBuffer.Release();
        sizeBuffer = new ComputeBuffer(6, sizeof(float));*/
        sizeBuffer.SetData(sizeBufferData);
        frustumCalc.SetBuffer("sizeBuffer", sizeBuffer);

        //更新buffer: indirect argument
        uint meshIndicesNum = grassMesh.GetIndexCount(0);

        //vertex count per instance, instance count, start vertex location, and start instance location
        uint[] args = new uint[5] { meshIndicesNum, 0, 0, 0, 0 };
        /*if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = new ComputeBuffer(1, sizeof(uint)*5,
            ComputeBufferType.IndirectArguments);*/
        argsBuffer.SetData(args);
        frustumCalc.SetBuffer("indirectDataBuffer", argsBuffer);

        //重新renderPosAppendBuffer
        if (renderPosAppendBuffer != null)
            renderPosAppendBuffer.Release();
        renderPosAppendBuffer = new ComputeBuffer(2048,
            sizeof(float) * 3, ComputeBufferType.Append);
        renderPosAppendBuffer.SetCounterValue(0);
        frustumCalc.SetBuffer("renderPosAppend", renderPosAppendBuffer);
        Shader.SetGlobalBuffer("renderPosAppend", renderPosAppendBuffer);

        //更新LOD用数据
        grassMaterial.SetInt("maxGrassCount", grassAmountPerTile);
        grassMaterial.SetInt("minGrassCount", minGrassPerTile);
        grassMaterial.SetFloat("zFar", Camera.main.farClipPlane);
        Vector3 pos = Camera.main.transform.position;
        grassMaterial.SetVector("camPos", new Vector4(pos.x, pos.y, pos.z, 0));

        frustumCalc.RunComputeShader();

#if false

        //bb的长度和renderPosAppendBuffer.count数量是不一致的
        List<Vector3> bb = new List<Vector3>();
        Array aa = System.Array.CreateInstance(typeof(Vector3), 2048);
        renderPosAppendBuffer.GetData(aa);

        for (int i =0; i < aa.Length; i++)
        {
            //bb[i] = (Vector3)aa.GetValue(i);
            if (((Vector3)aa.GetValue(i)).y > 0)
                bb.Add(((Vector3)aa.GetValue(i)));
        }
        bb.Sort((a, b) =>
        {
                if (a.x < b.x)
                    return -1;
                else if (Mathf.Approximately(a.x, b.x))
                {
                    if (a.z < b.z)
                        return -1;
                    else
                        return 1;
                }
                else
                    return 1;
        });

        string debugOutput = string.Empty;
        for (int i = 0; i < 30 && i < bb.Count; i++)
        {
            debugOutput += new Vector2(bb[i].x, bb[i].z).ToString("f0") + ", ";
        }

        //Debug.Log(debugOutput);

        
        //Debug.Log(renderPosAppendBuffer.count);
#endif

        ComputeBuffer.CopyCount(renderPosAppendBuffer, argsBuffer, 4);


        //render grass,TODO: LOD 64 32 16
        Graphics.DrawMeshInstancedIndirect(grassMesh,
            0, grassGen.grassMaterial, instanceBound, argsBuffer,0,null, UnityEngine.Rendering.ShadowCastingMode.TwoSided);

        if (mShowDrawCount)
        {
            mShowDrawCount = false;
            Array indirectArray = System.Array.CreateInstance(typeof(uint), 5);

            argsBuffer.GetData(indirectArray);
            mDrawCount = (uint)(indirectArray.GetValue(1));
        }

        ShowFPS();
    }

    private void ShowFPS() {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        float fpsT = 1.0f / deltaTime;
        fpsText.text = Mathf.Ceil(fpsT).ToString();

        countText.text = mDrawCount.ToString();
    }

    /*private void OnDrawGizmos() {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(instanceBound.center, instanceBound.size);
    }*/

    public void BuildTerrainTool() {
        terrainBuilder = new TerrainBuilder(heightMap, terrainHeight, terrainMat);
        GameObject terrain = GameObject.Find("terrain");
        if (terrain)
            DestroyImmediate(terrain);
        terrainBuilder.BuildTerrain(transform);
    }

    public void GenMathData() {
        RenderTexture data = new RenderTexture(128,
            bladeSectionCountMax - bladeSectionCountMin + 1,
            0, RenderTextureFormat.ARGB32);
        data.volumeDepth = bladeSectionCountMax + 1;
        data.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        data.enableRandomWrite = true;
        data.Create();
        
        int kernel = mathShader.FindKernel("GenMathData");
        mathShader.SetTexture(kernel, "Result", data);
        mathShader.Dispatch(kernel, 128 / 8, data.width, data.volumeDepth);
        grassMaterial.SetTexture("mathData", data);
        /*        if (SystemInfo.SupportsTextureFormat(TextureFormat.RFloat)) {
                    Texture3D data = new Texture3D(90,
                        bladeSectionCountMax - bladeSectionCountMin + 1,
                        bladeSectionCountMax, TextureFormat.RFloat, false);
                    int kernel = mathShader.FindKernel("GenMathData");
                    mathShader.SetTexture(kernel, "Result", data);

                } else {
                    Debug.Log("RFloat texture not supported!");
                }*/


    }

    void SetShadowTexture() {
        grassMaterial.SetTexture("_ShadowTex", shadowTex);
    }

    private void OnDisable() {
        grassGen.ReleaseBufer();
        //terrainBuilder.ReleaseBuffer();
        argsBuffer.Release();
        sizeBuffer.Release();
        renderPosAppendBuffer.Release();
    }
}
