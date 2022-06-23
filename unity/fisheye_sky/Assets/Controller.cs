using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;

public class Controller : MonoBehaviour
{
    public string host = "192.168.1.1";
    private int port = 2828;
    private UdpClient client;

    private float count = 0;
    // Start is called before the first frame update
    void Start()
    {
        count = 0;
        client = new UdpClient();
        client.Connect(host, port);

    }

    // Update is called once per frame
    void Update()
    {
        const float span = 50.0f/1000.0f;
        count += Time.deltaTime;
        if (count > span)
        {
            count -= span;
            float a = 0.5f+OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger)+ OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger);
            a /= 2.5f;
            Vector2 l = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
            Vector2 r = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
            string msg = String.Format("v {0:F3} {1:F3} {2:F3}", a*l.y * -1, a*l.x , a*r.x);

            Debug.Log(msg);
            var out_byte = Encoding.UTF8.GetBytes(msg);
            client.Send(out_byte, out_byte.Length);
        }
    }
}
