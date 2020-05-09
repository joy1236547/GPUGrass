using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//-----------------------------------------------------------------  
public class FirstPersonCameraControl : MonoBehaviour
{
    public Transform CameraTransform;
    public float moveSpeed = 30.0f;
    public float rotateSpeed = 0.2f;

    public static Vector3 kUpDirection = new Vector3(0.0f, 1.0f, 0.0f);

    public Joystick JoyStick;
    public Joystick CameraStick;


    //控制摄像机旋转的成员变量。  
    private float m_fLastMousePosX = 0.0f;
    private float m_fLastMousePosY = 0.0f;
    private bool m_bMouseRightKeyDown = false;


    CharacterController controller;
    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {

#if UNITY_EDITOR
        //判断旋转  
//         if (Input.GetMouseButtonDown(1)) //鼠标右键刚刚按下了  
//         {
//             if (m_bMouseRightKeyDown == false)
//             {
//                 m_bMouseRightKeyDown = true;
//                 Vector3 kMousePos = Input.mousePosition;
//                 m_fLastMousePosX = kMousePos.x;
//                 m_fLastMousePosY = kMousePos.y;
//             }
//         }
//         else if (Input.GetMouseButtonUp(1)) //鼠标右键刚刚抬起了  
//         {
//             if (m_bMouseRightKeyDown == true)
//             {
//                 m_bMouseRightKeyDown = false;
//                 m_fLastMousePosX = 0;
//                 m_fLastMousePosY = 0;
//             }
//         }
//         else if (Input.GetMouseButton(1)) //鼠标右键处于按下状态中  
//         {
//             if (m_bMouseRightKeyDown)
//             {
//                 Vector3 kMousePos = Input.mousePosition;
//                 float fDeltaX = kMousePos.x - m_fLastMousePosX;
//                 float fDeltaY = kMousePos.y - m_fLastMousePosY;
//                 m_fLastMousePosX = kMousePos.x;
//                 m_fLastMousePosY = kMousePos.y;
// 
// 
//                 Vector3 kNewEuler = CameraTransform.eulerAngles;
//                 kNewEuler.x += (fDeltaY * rotateSpeed);
//                 kNewEuler.y -= (fDeltaX * rotateSpeed);
//                 CameraTransform.eulerAngles = kNewEuler;
//             }
//         }


        //判断位移  
        float fMoveDeltaX = 0.0f;
        float fMoveDeltaZ = 0.0f;
        float fDeltaTime = Time.deltaTime;
        if (Input.GetKey(KeyCode.A))
        {
            fMoveDeltaX -= moveSpeed * fDeltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            fMoveDeltaX += moveSpeed * fDeltaTime;
        }
        if (Input.GetKey(KeyCode.W))
        {
            fMoveDeltaZ += moveSpeed * fDeltaTime;
        }
        if (Input.GetKey(KeyCode.S))
        {
            fMoveDeltaZ -= moveSpeed * fDeltaTime;
        }
        if (fMoveDeltaX != 0.0f || fMoveDeltaZ != 0.0f)
        {
            Vector3 kForward = transform.forward;
            Vector3 kRight = Vector3.Cross(kUpDirection, kForward);
            Vector3 kNewPos = transform.position;
            kNewPos += kRight * fMoveDeltaX;
            kNewPos += kForward * fMoveDeltaZ;

            controller.Move(kRight * fMoveDeltaX + kForward * fMoveDeltaZ + Vector3.down);
            //transform.position = kNewPos;
        }

#endif

        Vector3 dir = new Vector3(JoyStick.Direction.x, 0, JoyStick.Direction.y);
        Vector3 moveDelta = transform.TransformDirection(dir * moveSpeed * Time.deltaTime);
        moveDelta.y = 0;

        controller.Move(moveDelta + Vector3.down);

        //转镜头
        if(Mathf.Abs(CameraStick.Vertical) > 0 && Mathf.Abs(CameraStick.Horizontal) > 0)
        {
            float x = CameraTransform.eulerAngles.x;
            float y = transform.eulerAngles.y;
            x -= (CameraStick.Vertical * rotateSpeed);
            y += (CameraStick.Horizontal * rotateSpeed);
            if (x > 180)
                x -= 360;
            x = Mathf.Clamp(x , - 80, 80);
            transform.eulerAngles = new Vector3(0, y, 0);
            CameraTransform.localEulerAngles = new Vector3(x,0,0);

            //Debug.Log("Vertical: " + CameraStick.Vertical + "Horizontal: " + CameraStick.Horizontal);
        }



        //Debug.Log(JoyStick.Direction);
    }
}