using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform player;
    [SerializeField] private RectTransform radarRect;
    [SerializeField] private RectTransform dotsRoot;
    [SerializeField] private GameObject dotPrefab;

    [Header("scan")]
    [SerializeField] private LayerMask enemyMasl;
    [SerializeField] private float radarRangeWorld = 60f;
    [SerializeField] private int maxEnemies = 10;
    [SerializeField] private float refreshRate = 0.5f;

    [Header("Team Filter")]
    [SerializeField] private bool limitToSingleEnemyTeam = true;
    [SerializeField] private int trackedEnemyTeamId = 1;

    [Header("Mapping")]
    [SerializeField] private bool rotateWithPlayer = true;
    [SerializeField] private float edgePaddingPixels = 6f;
    [SerializeField] private bool showOUtOfRangeEdg = true;

    [Header("Flagship Marker")]
    [SerializeField] private bool highlightFlagships = true;
    [SerializeField] private float flagshipScaleMultiplier = 1.9f;
    [SerializeField] private Color flagshipDotColor = new Color(0.22f, 0.03f, 0.03f, 1f);

    private readonly Collider2D[] hits = new Collider2D[256];
    private readonly List<DotVisual> dotPool = new List<DotVisual>();

    private float timer;

    private sealed class DotVisual
    {
        public GameObject Root;
        public Transform Transform;
        public RectTransform Rect;
        public Image Image;
        public Color ImageBaseColor;
        public SpriteRenderer Sprite;
        public Color SpriteBaseColor;
    }

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
        TeamAgent playerTeamAgent = player.GetComponentInParent<TeamAgent>();
        int playerTeamId = playerTeamAgent != null ? playerTeamAgent.TeamId : 0;

        int count = Physics2D.OverlapCircleNonAlloc(p, radarRangeWorld, hits, enemyMasl);

        EnsurePool(Mathf.Min(count, maxEnemies));

        //Reset all dots to inactive before we set the active ones, this way we can reuse the pool without worrying about leftover active dots from previous frames
        for (int i = 0; i < dotPool.Count; i++)
            dotPool[i].Root.SetActive(false);

        float radiusPx = RadarRadiusPixels;
        float range = Mathf.Max(0.0001f, radarRangeWorld);

        float playerYaw = rotateWithPlayer ? player.eulerAngles.z : 0f;
        Quaternion invRot = Quaternion.Euler(0f, 0f, -playerYaw);

        int dotIndex = 0;
        for (int i = 0; i < count && dotIndex < maxEnemies; i++)
        {
            Collider2D c = hits[i];
            if (!c) continue;

            Transform t = c.transform;
            if (t == player) continue;

            TeamAgent targetTeamAgent = c.GetComponentInParent<TeamAgent>();
            if (targetTeamAgent == null)
                continue;

            int targetTeamId = targetTeamAgent.TeamId;
            if (limitToSingleEnemyTeam && targetTeamId != trackedEnemyTeamId)
                continue;

            if (!TeamRegistry.IsHostile(playerTeamId, targetTeamId))
                continue;

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
            Vector2 normalized = rotOffset / range;
            Vector2 posPx = normalized * radiusPx;

            DotVisual dot = dotPool[dotIndex];
            dotIndex++;
            dot.Root.SetActive(true);

            if (dot.Rect != null)
                dot.Rect.anchoredPosition = posPx;
            else if (dot.Transform != null)
                dot.Transform.localPosition = posPx;

            float baseScale = Mathf.Lerp(1.15f, 0.75f, dist / range);
            bool isFlagship = highlightFlagships && targetTeamAgent.GetComponentInParent<FlagshipController>() != null;
            ApplyDotStyle(dot, isFlagship, baseScale);
        }
    }

    private void ApplyDotStyle(DotVisual dot, bool isFlagship, float baseScale)
    {
        if (dot == null || dot.Transform == null)
            return;

        float scale = isFlagship
            ? baseScale * Mathf.Max(1f, flagshipScaleMultiplier)
            : baseScale;

        dot.Transform.localScale = new Vector3(scale, scale, 1f);

        if (dot.Image != null)
            dot.Image.color = isFlagship ? flagshipDotColor : dot.ImageBaseColor;

        if (dot.Sprite != null)
            dot.Sprite.color = isFlagship ? flagshipDotColor : dot.SpriteBaseColor;
    }

    private void EnsurePool(int needed)
    {
        while (dotPool.Count < needed && dotPool.Count < maxEnemies)
        {
            GameObject dot = Instantiate(dotPrefab, dotsRoot);

            DotVisual view = new DotVisual
            {
                Root = dot,
                Transform = dot.transform,
                Rect = dot.GetComponent<RectTransform>(),
                Image = dot.GetComponentInChildren<Image>(true),
                Sprite = dot.GetComponentInChildren<SpriteRenderer>(true)
            };

            if (view.Image != null)
                view.ImageBaseColor = view.Image.color;

            if (view.Sprite != null)
                view.SpriteBaseColor = view.Sprite.color;

            dotPool.Add(view);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        refreshRate = Mathf.Max(0f, refreshRate);
        radarRangeWorld = Mathf.Max(0f, radarRangeWorld);
        maxEnemies = Mathf.Clamp(maxEnemies, 1, 67);
        trackedEnemyTeamId = Mathf.Max(0, trackedEnemyTeamId);
        flagshipScaleMultiplier = Mathf.Max(1f, flagshipScaleMultiplier);
    }
#endif
}
