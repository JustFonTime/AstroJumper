using UnityEngine;

public class PlanetUIBtn : MonoBehaviour
{

    public GameObject planetUIGO;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnButtonClick()
    {
        Debug.Log("Planet UI Button Clicked");
        planetUIGO = GameObject.FindGameObjectsWithTag("PlanetUI")[0];
        Destroy(planetUIGO);
    }
}
