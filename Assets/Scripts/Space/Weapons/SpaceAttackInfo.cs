using System;
using UnityEngine;

[Serializable]
public class SpaceAttackInfo
{
    public string id;
    public GameObject projectile;
    public Transform firePoint;
    public float fireRate = 0.2f;
    public bool useSpread = false;
    public float minSpreadAngle = -2f;
    public float maxSpreadAngle = 2f;

    public bool canFire = true;
}
