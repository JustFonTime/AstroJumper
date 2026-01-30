using System.Collections.Generic;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlanetTracker : MonoBehaviour
{
    [SerializeField] GameObject UIpanelPrefab;
    [SerializeField] List<GameObject> planets = new List<GameObject>();
    Vector3 mousePos;
    [SerializeField] Vector3 mouseWorldPos;

    [SerializeField] bool onHover = false;
    [SerializeField] bool isHovering = false;

    private void Awake() 
    {
        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // get all planets in the scene using the tag system
        planets.AddRange(GameObject.FindGameObjectsWithTag("Planet"));

    }

    // Update is called once per frame
    void Update()
    {
        if (!onHover)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                OnClick();
            }   
            mousePos = Mouse.current.position.ReadValue();
            mousePos.z = Camera.main.nearClipPlane;
            mouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);

            // use raycast to see if it hits any planet colliders
            Ray ray = Camera.main.ScreenPointToRay(mouseWorldPos);
            Debug.DrawRay(mouseWorldPos, ray.direction * 10, Color.yellow);
            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, ray.direction * 10);
            if(hit.collider != null)
            {
                foreach(GameObject planet in planets)
                {
                    if(hit.collider.gameObject.name == planet.name)
                    {
                        // mouse is hovering over planet, do some stuff, rn make bigger
                        planet.transform.localScale = new Vector3(3, 3, 1);
                    }
                    
                }
            }
            else
            {
                // reset all planet scales
                foreach(GameObject planet in planets)
                {
                    planet.transform.localScale = new Vector3(2, 2, 1);
                }
            }
        }
        else
        {
            onPlanetHover();
        }
    }
    private void onPlanetHover()
    {
        mousePos = Mouse.current.position.ReadValue();
        mousePos.z = Camera.main.nearClipPlane;
        mouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);

        // use raycast to see if it hits any planet colliders
        Ray ray = Camera.main.ScreenPointToRay(mouseWorldPos);
        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, ray.direction * 10);
        if(hit.collider != null && !isHovering) 
        {
            foreach(GameObject planet in planets)
            {
                if(hit.collider.gameObject.name == planet.name)
                {
                    print("Clicked on " + planet.name);
                    // create UI panel showing planet info and get info from the planet script
                    var info = planet.GetComponent<Planet>().GetInfo(); // this holds all info about the planet
                    CreateUI(info.name, info.description, info.index, info.materials);
                    isHovering = true;
                }
            }
        }
        else if (hit.collider == null && isHovering)
        {
            isHovering = false;
            Destroy(GameObject.FindGameObjectWithTag("PlanetUI"));
        }
    }
    private void OnClick()
    {
        print("clicked");
        mousePos = Mouse.current.position.ReadValue();
        mousePos.z = Camera.main.nearClipPlane;
        mouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);

        // use raycast to see if it hits any planet colliders
        Ray ray = Camera.main.ScreenPointToRay(mouseWorldPos);
        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, ray.direction * 10);
        if(hit.collider != null)
        {
            foreach(GameObject planet in planets)
            {
                if(hit.collider.gameObject.name == planet.name)
                {
                    print("Clicked on " + planet.name);
                    // create UI panel showing planet info and get info from the planet script
                    var info = planet.GetComponent<Planet>().GetInfo(); // this holds all info about the planet
                    CreateUI(info.name, info.description, info.index, info.materials);

                }
            }
        }
    }

    void CreateUI(string name, string description, int index, string materials)
    {
        // create UI panel showing planet info
        Vector3 parentTransform = new Vector3(0, 0, 0);
        GameObject UI = Instantiate(UIpanelPrefab, parentTransform, Quaternion.identity);
        
        print(GameObject.FindGameObjectWithTag("Canvas").transform);


        UI.GetComponent<RectTransform>().SetParent(GameObject.FindGameObjectWithTag("Canvas").transform, false);


    }
}
