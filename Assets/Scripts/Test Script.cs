using UnityEngine;

public class TestScript : MonoBehaviour
{
    public GameObject direction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        direction.transform.LookAt(transform.position);



        RaycastHit hit;
        Physics.Raycast(direction.transform.position, direction.transform.forward, out hit);

        var dotValue = Vector3.Angle(hit.normal, direction.transform.forward);

        var pos = Vector3.Reflect(direction.transform.forward, hit.normal);

        GameObject obj = new GameObject("offpos");
        obj.transform.position = pos;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
