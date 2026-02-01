using System;
using TMPro;
using UnityEngine;

public class InfoManager : MonoBehaviour
{
    public TextAsset textAssetData;
    string infoText = "";
    public GameObject[] planets;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        planets = GameObject.FindGameObjectsWithTag("Planet");

        infoText = System.IO.File.ReadAllText(Application.dataPath + "/Level/Prefabs/Level Selector/Planet CSV/Astro Jumper Planets Data - Sheet1.csv");
        print(infoText);

        readCSV();
    }

    private void readCSV()
    {
        string[] data = textAssetData.text.Split(new char[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < planets.Length; i++)
        {
            Planet planet = planets[i].GetComponent<Planet>();
            planet.planetName = data[(i + 1)];
            planet.planetDescription = data[(i + 1) + 5 * 1];
            planet.resources = data[(i + 1) + 5 * 2];
            planet.dificulty = data[(i + 1) + 5 * 3];
            planet.faction = data[(i + 1) + 5 * 4];
            planet.displayName();
        }
    }
}
