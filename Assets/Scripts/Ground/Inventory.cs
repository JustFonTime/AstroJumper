using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public List<Item> items = new List<Item>();
    public static event Action<Item> OnItemAdded;
    public static event Action OnInventoryChanged;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AddItem(Item item)
    {
        items.Add(item);
        OnItemAdded?.Invoke(item);
        OnInventoryChanged?.Invoke();
    }

    public int GetScrapCount()
    {
        int scrapCount = 0;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is Scrap)
            {
                scrapCount++;
            }
        }

        return scrapCount;
    }

    public void ClearScrap()
    {
        int removedCount = items.RemoveAll(item => item is Scrap);
        if (removedCount > 0)
        {
            OnInventoryChanged?.Invoke();
        }
    }
}
