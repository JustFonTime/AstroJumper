using UnityEngine;

public class Planet : MonoBehaviour
{
    string planetName = "Cool Planet";
    string planetDescription = "This is a very cool planet.";
    int planetIndex = 1;
    string materials = "Iron, Emerald";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public (string name, string description, int index, string materials) GetInfo()
    {
        return (planetName, planetDescription, planetIndex, materials);
    }
}
