using Unity.VisualScripting;
using UnityEngine;

public class PlanetBtn : MonoBehaviour
{

    [SerializeField] GameObject planetUIPrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void onClick()
    {
        Vector3 parentTransform = new Vector3(0, 0, 0);
        GameObject UI = Instantiate(planetUIPrefab, parentTransform, Quaternion.identity);
        
        print(GameObject.FindGameObjectWithTag("Canvas").transform);


        UI.GetComponent<RectTransform>().SetParent(GameObject.FindGameObjectWithTag("Canvas").transform, false);
    }
}
