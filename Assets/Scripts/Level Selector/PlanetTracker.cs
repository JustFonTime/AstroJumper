using System.Collections.Generic;
using System.Collections;
using NUnit.Framework;
using TMPro;
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
    [SerializeField] Vector3 planetBaseScale = new Vector3(2,2,1);
    [SerializeField] Vector3 planetExpandedScale = new Vector3(3,3,1);
    [SerializeField] float expandTime = 1f;
    [SerializeField]private string planetExpanding = "";

    private void Awake() 
    {
        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // get all planets in the scene using the tag system
        planets.AddRange(GameObject.FindGameObjectsWithTag("Planet"));
    }
    void Update() 
    {
        if (GameObject.FindAnyObjectByType<PlanetUIBtn>() != null)
        {
            return;
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
                    planetExpanding = planet.name;
                    expandPlanet(planet);
                }
            }
        }
        // else
        // {
            
            // reset all planet scales
            planetExpanding = "";
            foreach(GameObject planet in planets)
            {
                if(planet.transform.localScale != planetBaseScale)
                {
                    shrinkPlanet(planet);
                }
            }
        //}

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnClick();
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
                // reset all planet scales
                planet.transform.localScale = new Vector3(2, 2, 1);
                if(hit.collider.gameObject.name == planet.name)
                {
                    print("Clicked on " + planet.name);

                    // create UI panel showing planet info and get info from the planet script
                    CreateUI(planet);

                }
            }
        }
    }

    void CreateUI(GameObject planetGO)
    {
        // create UI panel showing planet info
        Vector3 parentTransform = new Vector3(0, 0, 0);
        GameObject UI = Instantiate(UIpanelPrefab, parentTransform, Quaternion.identity);
        
        print(GameObject.FindGameObjectWithTag("Canvas").transform);

        TextMeshProUGUI UIText = UI.GetComponentInChildren<TextMeshProUGUI>();
        Planet planet = planetGO.GetComponent<Planet>();
        UIText.text = planet.planetName + "\n" + planet.dificulty + "\n" + planet.faction + "\n" + planet.resources;

        UI.GetComponent<RectTransform>().SetParent(GameObject.FindGameObjectWithTag("Canvas").transform, false);


    }

    public void expandPlanet(GameObject planet)
    {
        var startScale = planet.transform.localScale;
        var endScale = planetExpandedScale;

        planet.transform.localScale = Vector3.Lerp(startScale, endScale, expandTime * Time.deltaTime);

   
        
    }

    public void shrinkPlanet(GameObject planet)
    {
        var startScale = planet.transform.localScale;
        var endScale = planetBaseScale;

        planet.transform.localScale = Vector3.Lerp(startScale, endScale, expandTime * Time.deltaTime);


    }
}
