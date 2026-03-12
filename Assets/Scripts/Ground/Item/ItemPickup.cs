using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public Item item;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        print("Collided with: " + collision.gameObject.name);
        if (collision.CompareTag("Player"))
        {
            Inventory inventory = collision.GetComponent<Inventory>();
            if (inventory != null)
            {
                // Assuming this GameObject has an Item component attached to it
                if (item != null)
                {
                    inventory.AddItem(item);
                    Destroy(gameObject); // Destroy the pickup after adding it to the inventory
                }
            }
        }
    }
}
