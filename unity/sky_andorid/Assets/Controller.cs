using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using UnityEngine.InputSystem;

public class Controller : MonoBehaviour
{
    public string host = "192.168.1.1";
    private int port = 2828;
    private UdpClient client;
    public float acc = 0.2f;
    private float count = 0;
    // Start is called before the first frame update
    void Start()
    {
        count = 0;
        client = new UdpClient();
        client.Connect(host, port);

    }
    public void SetAcc(float a)
    {
        acc = Mathf.Clamp(a,0,1);
    }
    // Update is called once per frame
    void Update()
    {
        const float span = 50.0f/1000.0f;
        count += Time.deltaTime;
        if (count > span)
        {
            count -= span;
            Vector2 r = Gamepad.current.rightStick.ReadValue();
            Vector2 l = Gamepad.current.leftStick.ReadValue();
            string msg = String.Format("v {0:F3} {1:F3} {2:F3}", acc*(r.y+l.y) * -1, acc*r.x , acc*l.x);
            var out_byte = Encoding.UTF8.GetBytes(msg);
            client.Send(out_byte, out_byte.Length);
            
        }
    }
}
