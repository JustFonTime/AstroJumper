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
    [SerializeField] private bool alwaysShowFlagships = true;
    [SerializeField] private bool pinFlagshipsToEdge = true;
    [SerializeField] private float flagshipScaleMultiplier = 1.9f;
    [SerializeField] private Color flagshipDotColor = new Color(0.22f, 0.03f, 0.03f, 1f);
    [SerializeField] private Color friendlyFlagshipDotColor = new Color(0.14f, 0.82f, 0.31f, 1f);

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
        Vector2 playerPosition = player.position;
        TeamAgent playerTeamAgent = player.GetComponentInParent<TeamAgent>();
        int playerTeamId = playerTeamAgent != null ? playerTeamAgent.TeamId : 0;

        ResolveFlagships(playerTeamId, out FlagshipController friendlyFlagship, out FlagshipController hostileFlagship);

        int hitCount = Physics2D.OverlapCircleNonAlloc(playerPosition, radarRangeWorld, hits, enemyMasl);
        int regularEnemyCount = CountRegularEnemyDots(hitCount, playerTeamId);
        int flagshipCount = GetVisibleFlagshipCount(friendlyFlagship, hostileFlagship);

        EnsurePool(Mathf.Min(regularEnemyCount, maxEnemies) + flagshipCount);

        for (int i = 0; i < dotPool.Count; i++)
            dotPool[i].Root.SetActive(false);

        float radiusPx = RadarRadiusPixels;
        float range = Mathf.Max(0.0001f, radarRangeWorld);
        float playerYaw = rotateWithPlayer ? player.eulerAngles.z : 0f;
        Quaternion invRot = Quaternion.Euler(0f, 0f, -playerYaw);

        int dotIndex = 0;
        int enemyDotsShown = 0;
        for (int i = 0; i < hitCount && enemyDotsShown < maxEnemies; i++)
        {
            if (!TryGetRegularEnemyTarget(hits[i], playerTeamId, out Transform targetTransform))
                continue;

            Vector2 offset = (Vector2)targetTransform.position - playerPosition;
            float dist = offset.magnitude;
            bool clampToEdge = false;

            if (dist > range)
            {
                if (!showOUtOfRangeEdg) continue;
                clampToEdge = true;
                dist = range;
            }

            float baseScale = clampToEdge ? 0.75f : Mathf.Lerp(1.15f, 0.75f, dist / range);
            AddDot(ref dotIndex, offset, range, radiusPx, invRot, false, clampToEdge, baseScale, false, Color.clear);
            enemyDotsShown++;
        }

        AddFlagshipDot(ref dotIndex, friendlyFlagship, playerPosition, range, radiusPx, invRot, friendlyFlagshipDotColor);
        AddFlagshipDot(ref dotIndex, hostileFlagship, playerPosition, range, radiusPx, invRot, flagshipDotColor);
    }

    private int CountRegularEnemyDots(int hitCount, int playerTeamId)
    {
        int regularEnemyCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            if (TryGetRegularEnemyTarget(hits[i], playerTeamId, out _))
                regularEnemyCount++;
        }

        return regularEnemyCount;
    }

    private int GetVisibleFlagshipCount(FlagshipController friendlyFlagship, FlagshipController hostileFlagship)
    {
        if (!highlightFlagships || !alwaysShowFlagships)
            return 0;

        int count = 0;
        if (friendlyFlagship != null)
            count++;
        if (hostileFlagship != null)
            count++;
        return count;
    }

    private bool TryGetRegularEnemyTarget(Collider2D colliderHit, int playerTeamId, out Transform targetTransform)
    {
        targetTransform = null;
        if (!colliderHit)
            return false;

        TeamAgent targetTeamAgent = colliderHit.GetComponentInParent<TeamAgent>();
        if (targetTeamAgent == null)
            return false;

        targetTransform = targetTeamAgent.transform;
        if (targetTransform == player)
            return false;

        int targetTeamId = targetTeamAgent.TeamId;
        if (limitToSingleEnemyTeam && targetTeamId != trackedEnemyTeamId)
            return false;

        if (!TeamRegistry.IsHostile(playerTeamId, targetTeamId))
            return false;

        if (alwaysShowFlagships && highlightFlagships && targetTeamAgent.GetComponentInParent<FlagshipController>() != null)
            return false;

        return true;
    }

    private void ResolveFlagships(int playerTeamId, out FlagshipController friendlyFlagship, out FlagshipController hostileFlagship)
    {
        friendlyFlagship = null;
        hostileFlagship = null;

        if (!highlightFlagships)
            return;

        FlagshipController[] flagships = FindObjectsOfType<FlagshipController>(true);
        for (int i = 0; i < flagships.Length; i++)
        {
            FlagshipController flagship = flagships[i];
            if (flagship == null || !flagship.isActiveAndEnabled)
                continue;

            int flagshipTeamId = GetFlagshipTeamId(flagship);
            if (flagshipTeamId < 0)
                continue;

            if (flagshipTeamId == playerTeamId)
            {
                friendlyFlagship = ChoosePreferredFlagship(friendlyFlagship, flagship);
                continue;
            }

            if (!TeamRegistry.IsHostile(playerTeamId, flagshipTeamId))
                continue;

            if (limitToSingleEnemyTeam && flagshipTeamId != trackedEnemyTeamId)
                continue;

            hostileFlagship = ChoosePreferredFlagship(hostileFlagship, flagship);
        }
    }

    private int GetFlagshipTeamId(FlagshipController flagship)
    {
        if (flagship == null)
            return -1;

        if (flagship.TeamAgent != null)
            return flagship.TeamAgent.TeamId;

        return flagship.TryGetComponent(out TeamAgent teamAgent) ? teamAgent.TeamId : -1;
    }

    private FlagshipController ChoosePreferredFlagship(FlagshipController current, FlagshipController candidate)
    {
        if (candidate == null)
            return current;

        if (current == null)
            return candidate;

        if (current.CurrentState == FlagshipController.BattleState.Destroyed &&
            candidate.CurrentState != FlagshipController.BattleState.Destroyed)
        {
            return candidate;
        }

        return current;
    }

    private void AddFlagshipDot(
        ref int dotIndex,
        FlagshipController flagship,
        Vector2 playerPosition,
        float range,
        float radiusPx,
        Quaternion invRot,
        Color dotColor)
    {
        if (!highlightFlagships || !alwaysShowFlagships || flagship == null)
            return;

        Vector2 offset = (Vector2)flagship.transform.position - playerPosition;
        bool clampToEdge = pinFlagshipsToEdge;
        float dist = offset.magnitude;

        if (!clampToEdge && dist > range)
        {
            if (!showOUtOfRangeEdg)
                return;

            clampToEdge = true;
        }

        AddDot(ref dotIndex, offset, range, radiusPx, invRot, true, clampToEdge, 1f, true, dotColor);
    }

    private void AddDot(
        ref int dotIndex,
        Vector2 worldOffset,
        float range,
        float radiusPx,
        Quaternion invRot,
        bool isFlagship,
        bool clampToEdge,
        float baseScale,
        bool useCustomColor,
        Color customColor)
    {
        if (dotIndex >= dotPool.Count)
            return;

        Vector2 mappedOffset = worldOffset;
        if (clampToEdge)
        {
            Vector2 direction = worldOffset.sqrMagnitude > 0.0001f ? worldOffset.normalized : Vector2.up;
            mappedOffset = direction * range;
        }

        Vector2 rotOffset = (Vector2)(invRot * (Vector3)mappedOffset);
        Vector2 posPx = (rotOffset / range) * radiusPx;

        DotVisual dot = dotPool[dotIndex];
        dotIndex++;
        dot.Root.SetActive(true);

        if (dot.Rect != null)
            dot.Rect.anchoredPosition = posPx;
        else if (dot.Transform != null)
            dot.Transform.localPosition = posPx;

        ApplyDotStyle(dot, isFlagship, baseScale, useCustomColor, customColor);
    }

    private void ApplyDotStyle(DotVisual dot, bool isFlagship, float baseScale, bool useCustomColor, Color customColor)
    {
        if (dot == null || dot.Transform == null)
            return;

        float scale = isFlagship
            ? baseScale * Mathf.Max(1f, flagshipScaleMultiplier)
            : baseScale;

        dot.Transform.localScale = new Vector3(scale, scale, 1f);

        Color imageColor = useCustomColor
            ? customColor
            : isFlagship ? flagshipDotColor : dot.ImageBaseColor;
        Color spriteColor = useCustomColor
            ? customColor
            : isFlagship ? flagshipDotColor : dot.SpriteBaseColor;

        if (dot.Image != null)
            dot.Image.color = imageColor;

        if (dot.Sprite != null)
            dot.Sprite.color = spriteColor;
    }

    private void EnsurePool(int needed)
    {
        while (dotPool.Count < needed)
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
