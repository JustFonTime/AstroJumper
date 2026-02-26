using System;
using System.Collections.Generic;
using UnityEngine;

public class RadarUI : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private Transform player;

    [SerializeField] private RectTransform radarRect;
    [SerializeField] private RectTransform dotsRoot; // parent for the dots
    [SerializeField] private GameObject dotPrefab; // prefab for the dots

    [Header("scan")] [SerializeField] private LayerMask enemyMasl;
    [SerializeField] private float radarRangeWorld = 60f; // range in world units
    [SerializeField] private int maxEnemies = 10; // max number of enemies to show on radar
    [SerializeField] private float refreshRate = 0.5f; // how often to refresh the radar

    [Header("Mapping")] [SerializeField]
    private bool rotateWithPlayer = true; // should the radar rotate with the player?

    [SerializeField] private float edgePaddingPixels = 6f;
    [SerializeField] private bool showOUtOfRangeEdg = true;

    private readonly Collider2D[] hits = new Collider2D[256];
    private readonly List<GameObject> dotPool = new List<GameObject>();

    private float timer;

    private float RadarRadiusPixels
    {
        get
        {
            //asume radarRect is a square, so we take the smaller of width and height, divide by 2 for radius, and subtract edge padding
            return Mathf.Min(radarRect.rect.width, radarRect.rect.height) / 2f - edgePaddingPixels;
        }
    }

    private void Awake()
    {
        if (!radarRect) radarRect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (!player) return;

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = refreshRate;

        RefreshDots();
    }

    private void RefreshDots()
    {
        Vector2 p = player.position;

        int count = Physics2D.OverlapCircleNonAlloc(p, radarRangeWorld, hits, enemyMasl);

        EnsurePool(Mathf.Min(count, maxEnemies));


        //Reset all dots to inactive before we set the active ones, this way we can reuse the pool without worrying about leftover active dots from previous frames
        for (int i = 0; i < dotPool.Count; i++)
            dotPool[i].gameObject.SetActive(false);


        float radiusPx = RadarRadiusPixels;
        float range = Mathf.Max(0.0001f, radarRangeWorld);

        float playuerYaw = rotateWithPlayer ? player.eulerAngles.z : 0f;
        Quaternion invRot = Quaternion.Euler(0f, 0f, -playuerYaw);


        int dotIndex = 0;
        for (int i = 0; i < count && dotIndex < maxEnemies; i++)
        {
            var c = hits[i];
            if (!c) continue;

            Transform t = c.transform;
            if (t == player) continue;

            Vector2 offset = (Vector2)t.position - p;

            float dist = offset.magnitude;

            //if enemy is out of radar range, we clamp the offset to the edge of the radar
            if (dist > radarRangeWorld)
            {
                if (!showOUtOfRangeEdg) continue;

                offset = offset.normalized * radarRangeWorld;
                dist = radarRangeWorld;
            }

            Vector2 rotOffset = (Vector2)(invRot * (Vector3)offset);

            Vector2 normalized = rotOffset / range; // 0 to 1 based on radar range
            Vector2 posPx = normalized * radiusPx; // position in pixels on the radar

            //Activate dot
            GameObject dot = dotPool[dotIndex];
            dotIndex++;
            dot.gameObject.SetActive(true);

            RectTransform rt = dot.GetComponent<RectTransform>();
            rt.anchoredPosition = posPx;

            //Scale dot slightly by distance
            float s = Mathf.Lerp(1.15f, 0.75f, dist / radarRangeWorld);
            rt.localScale = new Vector3(s, s, 1f);
        }
    }

    private void EnsurePool(int needed)
    {
        while (dotPool.Count < needed && dotPool.Count < maxEnemies)
        {
            GameObject dot = Instantiate(dotPrefab, dotsRoot);
            dotPool.Add(dot);
        }
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        refreshRate = Mathf.Max(0f, refreshRate);
        radarRangeWorld = Mathf.Max(0f, radarRangeWorld);
        maxEnemies = Mathf.Clamp(maxEnemies, 1, 67);
    }
#endif
}