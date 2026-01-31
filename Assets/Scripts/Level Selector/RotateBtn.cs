using UnityEngine;

public class RotateBtn : MonoBehaviour
{
    [SerializeField] private bool rotateRight = false;
    private int whichPlanet = 0;
    public GameObject[] planets;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        planets = GameObject.FindGameObjectsWithTag("Planet");
        selectPlanet(whichPlanet);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void rotatePlanet()
    {
        if (rotateRight)
        {
            whichPlanet++;
            if (whichPlanet >= planets.Length)
            {
                whichPlanet = 0;
            }
            selectPlanet(whichPlanet);
        }
        else
        {
            whichPlanet--;
            if (whichPlanet < 0)
            {
                whichPlanet = planets.Length - 1;
            }
            selectPlanet(whichPlanet);
        }
    }

    public void selectPlanet(int planetIndex)
    {
        // place the selected planet in the center move all the others off the screen
        for (int i = 0; i < planets.Length; i++)
        {
            if (i == planetIndex)
            {
                planets[i].transform.position = new Vector3(0, 1.4f, 0);
                // call function which loads all data into the UI
            }
            else
            {
                planets[i].transform.position = new Vector3(1000, 1000, 1000);
            }
        }
    }
}
