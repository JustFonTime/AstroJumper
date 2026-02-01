using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Planet : MonoBehaviour
{
    public string planetName;
    public string planetDescription;
    public string resources;
    public string dificulty;
    public string faction;
    private GameObject nameText;
    [SerializeField] GameObject nameTextPrefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void displayName()
    {
        print("Displaying name for " + planetName);
        Vector3 textPos = Camera.main.WorldToScreenPoint(transform.position + new Vector3(2f, -1.5f, 0));
        nameText = Instantiate(nameTextPrefab, textPos, Quaternion.identity, GameObject.FindGameObjectWithTag("Canvas").transform);
        nameText.GetComponent<TextMeshProUGUI>().text = planetName;

        nameText.GetComponent<RectTransform>().SetParent(GameObject.FindGameObjectWithTag("Canvas").transform, false);

        nameText.GetComponent<PlanetText>().planetGO = this.gameObject;
    }
}
