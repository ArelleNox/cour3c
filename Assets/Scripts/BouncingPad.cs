using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class BouncingPad : MonoBehaviour
{
    [SerializeField] private float force;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Rigidbody>() != null)
        {
            other.GetComponent<Rigidbody>().AddForce(Vector3.up * force, ForceMode.Impulse);
        }
    }
}
