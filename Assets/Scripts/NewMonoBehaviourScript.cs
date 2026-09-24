using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("start ");
    }

    private void Awake()
    {
        Debug.Log("awake ");
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log("update " + Time.deltaTime);
    }
}
