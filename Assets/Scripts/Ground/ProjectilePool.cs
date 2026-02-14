using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{

    public GameObject projectilePrefab;
    public int poolSize = 10;
    public Queue<GameObject> projectilePool = new Queue<GameObject>();

    void Awake()
    {
        projectilePrefab = GetComponentInParent<Unit>().GetProjectilePrefab();
        if(projectilePrefab == null)
        {
            print("Add a attack thats a projectile");
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public GameObject GetProjectile()
    {
        if (projectilePool.Count > 0)
        {
            GameObject projectile = projectilePool.Dequeue();
            projectile.SetActive(true);
            projectile.GetComponent<Projectile>().enabled = true; 
            projectile.transform.GetChild(0).gameObject.SetActive(true); 
            return projectile;
        }
        else
        {
            return Instantiate(projectilePrefab);
        }
    }

    public void ReturnProjectile(GameObject projectile)
    {
        projectile.SetActive(false);
        projectile.GetComponent<Projectile>().enabled = false;
        projectile.transform.GetChild(0).gameObject.SetActive(false); 
        projectilePool.Enqueue(projectile);
    }
}
