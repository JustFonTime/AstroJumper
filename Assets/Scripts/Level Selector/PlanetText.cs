using UnityEngine;

public class PlanetText : MonoBehaviour
{
    [SerializeField] public GameObject planetGO;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        UpdateTransform();   
    }

    void UpdateTransform()
    {
        Vector3 textPos = planetGO.transform.position + new Vector3(0.8f, -1.5f, 0);
        transform.position = textPos;
    }
}
