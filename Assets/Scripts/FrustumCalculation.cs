﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrustumCalculation {
    private ComputeShader calcShader;
    private TerrainBuilder tBuilder;

    private Vector2Int texSize = new Vector2Int();
    private Vector2Int threadGroupSize = new Vector2Int(2, 2);
    private int frustumKernel = 0;
    //public GameObject plane;//for test
    private RenderTexture frustumTexture;//有用但不必放在这里，是为了测试

    //private Bounds boundMin;//test


    /// <summary>
    /// 返回threadSize(tex大小)+起始位置（index,not constrained）
    /// </summary>
    public Bounds PrepareCamData(Camera camera, out Vector4 threadSize) {
        Vector3[] frustum = new Vector3[3];
        #region 获取视锥体frustum
        float halfFOV = (camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;//竖着的
        float height = camera.farClipPlane * Mathf.Tan(halfFOV);
        float width = height * camera.aspect;

        Vector3 vec = camera.transform.position, widthDelta = camera.transform.right * width;
        vec -= widthDelta;
        vec += (camera.transform.forward * camera.farClipPlane);
        frustum[0] = camera.transform.position - camera.transform.forward * 5;
        frustum[1] = vec - camera.transform.right * 3;
        frustum[2] = vec + 2 * widthDelta + camera.transform.right * 3;
        #endregion
        Bounds camBound = new Bounds(frustum[0], Vector3.zero);//相机包围盒
        for (int i = 1; i < frustum.Length; i++) {
            camBound.Encapsulate(frustum[i]);
        }
        Vector2Int minIndex = tBuilder.GetConstrainedTileIndex(camBound.min),
            maxIndex = tBuilder.GetConstrainedTileIndex(camBound.max);

        //更新compute shader
        int digit = 0, newTexSizeX, newTexSizeY;
        #region 将frustumTexSize向上取整（二进制），eg: 9→16
        newTexSizeX = maxIndex.x - minIndex.x + 1;
//         for (int i = 31; i >= 0; i--)
//             if (((newTexSizeX >> i) & 1) == 1) { digit = i + 1; break; }
//         newTexSizeX = 1 << digit;
        newTexSizeY = maxIndex.y - minIndex.y + 1;
//         for (int i = 31; i >= 0; i--)
//             if (((newTexSizeY >> i) & 1) == 1) { digit = i + 1; break; }
//         newTexSizeY = 1 << digit;
        #endregion
        #region for test
        /*if (newTexSizeX != texSize.x || newTexSizeY != texSize.y) {
            //new testTexture texture
            //for test
            frustumTexture = new RenderTexture(newTexSizeX, newTexSizeY, 24);
            frustumTexture.enableRandomWrite = true;
            frustumTexture.Create();
            calcShader.SetTexture(frustumKernel, "testTexture", frustumTexture);
            //plane.GetComponent<Renderer>().sharedMaterial.mainTexture = frustumTexture;
        }*/
        #endregion
        texSize.x = newTexSizeX; texSize.y = newTexSizeY;

        //想传6个整数，但unity的computeShader.SetInts和SetFloats有问题
        //         Vector4[] frusIndex = { Vector4.zero, Vector4.zero };
        //         for (int i = 0; i < 3; i++) {
        //             Vector2Int t = tBuilder.GetTileIndex(frustum[i]);
        //             //t -= tBuilder.GetTileIndex(camBound.min);
        //             frusIndex[i / 2] += new Vector4(t.x * Mathf.Abs(i - 1), t.y * Mathf.Abs(i - 1),
        //                 t.x * (i % 2), t.y * (i % 2));
        //         }

        //数据需要16-bytes对齐
        //参考：https://docs.microsoft.com/zh-cn/windows/win32/direct3dhlsl/dx-graphics-hlsl-packing-rules
        int[] frustIndex = { 
            tBuilder.GetTileIndex(frustum[0]).x, tBuilder.GetTileIndex(frustum[0]).y, 0,0,
            tBuilder.GetTileIndex(frustum[1]).x, tBuilder.GetTileIndex(frustum[1]).y, 0,0,
            tBuilder.GetTileIndex(frustum[2]).x, tBuilder.GetTileIndex(frustum[2]).y, 0,0};


        //Debug.Log(frusIndex[0].ToString() + "   " + frusIndex[1].ToString());
        calcShader.SetInts("frustumPosIndex", frustIndex);
        var f = tBuilder.GetTileIndex(camBound.min);

        threadSize = new Vector4(texSize.x, texSize.y,f.x,f.y);
        return camBound;
    }

    public void RunComputeShader() {
        calcShader.Dispatch(frustumKernel, texSize.x / threadGroupSize.x,
            texSize.y / threadGroupSize.y, 1);
    }
    

    public FrustumCalculation(ComputeShader shader, TerrainBuilder t) {
        tBuilder = t; calcShader = shader;
        //setup
        frustumKernel = calcShader.FindKernel("FrustumCulling");
        
        //test
        //plane.GetComponent<Renderer>().sharedMaterial.mainTexture = frustumTexture;
    }
    
    public void SetBuffer(string name, ComputeBuffer buffer) {
        calcShader.SetBuffer(frustumKernel, name, buffer);
    }

    public void SetFloat(string name, float value) {
        calcShader.SetFloat(name, value);
    }

    public void SetTextureFromGlobal(string name) {
        calcShader.SetTextureFromGlobal(frustumKernel, name, name);
    }
}
