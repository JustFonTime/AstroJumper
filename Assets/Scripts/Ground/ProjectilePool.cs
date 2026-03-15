using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{

    public GameObject projectilePrefab;
    public int poolSize = 10;
    public Queue<GameObject> projectilePool = new Queue<GameObject>();
    public ProjectileAudio playSound;

    void Awake()
    {
        projectilePrefab = GetComponentInParent<Unit>().GetProjectilePrefab();
        if(projectilePrefab == null)
        {
            print("Add a attack thats a projectile");
        }
    }

    public GameObject GetProjectile()
    {
        if (projectilePool.Count > 0)
        {
            GameObject projectile = projectilePool.Dequeue();
            projectile.SetActive(true);
            projectile.GetComponent<Projectile>().enabled = true; 
            projectile.transform.GetChild(0).gameObject.SetActive(true);
            playSound.PlayRandomSound();
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
