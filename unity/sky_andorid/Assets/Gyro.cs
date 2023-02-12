using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gyro : MonoBehaviour
{
    private Camera cam;
    private void Awake()
    {
        Application.targetFrameRate = 30;
    }
    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        Input.gyro.enabled = true;
    }
    public void SetFOV(float p)
    {
        cam.fieldOfView = Mathf.Clamp(p,10,170);
    }
    // Update is called once per frame
    void Update()
    {
        //transform.rotation = Quaternion.Euler(0, 0, -180) * Quaternion.Euler(-90, 0, 0) * Input.gyro.attitude * Quaternion.Euler(0, 0, 180);
        Quaternion q= Input.gyro.attitude;
        var newQ = new Quaternion(-q.x, -q.z, -q.y, q.w);
        transform.rotation = Quaternion.Euler(0, -90,0) * newQ * Quaternion.Euler(90, 0, 0);

    }
}
