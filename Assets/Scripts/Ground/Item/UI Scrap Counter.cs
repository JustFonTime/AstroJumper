using TMPro;
using UnityEngine;

public class UIScrapCounter : MonoBehaviour
{
    public TextMeshProUGUI scrapCountText;
    public void OnEnable()
    {
        Inventory.OnItemAdded += UpdateScrapCount;
    }

    public void OnDisable()
    {
        Inventory.OnItemAdded -= UpdateScrapCount;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void UpdateScrapCount(Item item)
    {
        if (item is Scrap)
        {
            // Update the UI to reflect the new scrap count
            print("Scrap added: " + item.itemName);
            scrapCountText.text = FindFirstObjectByType<Inventory>().items.FindAll(i => i is Scrap).Count.ToString();
        }
    }
}
