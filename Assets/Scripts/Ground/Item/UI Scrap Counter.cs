using TMPro;
using UnityEngine;

public class UIScrapCounter : MonoBehaviour
{
    public TextMeshProUGUI scrapCountText;
    private Inventory inventory;

    public void OnEnable()
    {
        Inventory.OnInventoryChanged += RefreshScrapCount;
    }

    public void OnDisable()
    {
        Inventory.OnInventoryChanged -= RefreshScrapCount;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inventory = FindFirstObjectByType<Inventory>();
        RefreshScrapCount();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void RefreshScrapCount()
    {
        if (scrapCountText == null)
            return;

        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        scrapCountText.text = inventory != null ? inventory.GetScrapCount().ToString() : "0";
    }
}
