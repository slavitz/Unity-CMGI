using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamFlythrough : MonoBehaviour
{
    public float mainSpeed = 100.0f; //regular speed
    public float shiftAdd  = 250.0f; //multiplied by how long shift is held.  Basically running
    public float maxShift  = 1000.0f; //Maximum speed when holdin gshift
    public float camSens  = 0.25f; //How sensitive it with mouse
    private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
    private float totalRun  = 1.0f;
    float X = 0;
    float Y = 0;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        const float MIN_X = 0.0f;
        const float MAX_X = 360.0f;
        const float MIN_Y = -90.0f;
        const float MAX_Y = 90.0f;

        X += Input.GetAxis("Mouse X") * (camSens * Time.deltaTime);
        if (X < MIN_X) X += MAX_X;
        else if (X > MAX_X) X -= MAX_X;
        Y -= Input.GetAxis("Mouse Y") * (camSens * Time.deltaTime);
        if (Y < MIN_Y) Y = MIN_Y;
        else if (Y > MAX_Y) Y = MAX_Y;

        transform.rotation = Quaternion.Euler(Y, X, 0.0f);
        //Mouse  camera angle done.  

        //Keyboard commands
        float f = 0.0f;
        var p = GetBaseInput();
        if (Input.GetKey(KeyCode.LeftShift))
        {
            totalRun += Time.deltaTime;
            p = p * totalRun * shiftAdd;
            p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
            p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
            p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
        }
        else
        {
            totalRun = Mathf.Clamp(totalRun * 0.5f, 1, 1000);
            p = p * mainSpeed;
        }

        p = p * Time.deltaTime;
        if (Input.GetKey(KeyCode.Space))
        {
            f = transform.position.y;
            transform.Translate(p);
            Vector3 pos = transform.position;
            pos.y = f;
            transform.position = pos;
        }
        else
        {
            transform.Translate(p);
        }

    }

    private Vector3 GetBaseInput()
    { 
        Vector3 p_Velocity = Vector3.zero;

        if (Input.GetKey (KeyCode.W))
        {
            p_Velocity += new Vector3(0, 0 , 1);
        }
        if (Input.GetKey(KeyCode.S))
        {
            p_Velocity += new Vector3(0, 0, -1);
        }
        if (Input.GetKey(KeyCode.A))
        {
            p_Velocity += new Vector3(-1, 0, 0);
        }
        if (Input.GetKey(KeyCode.D))
        {
            p_Velocity += new Vector3(1, 0, 0);
        }
        return p_Velocity;
    }
}
