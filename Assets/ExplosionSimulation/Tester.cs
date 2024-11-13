using UnityEngine;
using UnityEngine.UI;

public class Tester : MonoBehaviour
{
    public bool isActive = false;
    public string textureName;
    // Update is called once per frame
    void Update()
    {
        if (isActive)
            Test();
    }



    RawImage rawImage;

    void Test()
    {
        GameObject d = GameObject.Find("Draw");
        var mat = d.GetComponent<MeshRenderer>().sharedMaterial;

        for (int i = 0; i < mat.passCount; i++)
        {
            Debug.Log(mat.GetPassName(i));
        }
    }
}
