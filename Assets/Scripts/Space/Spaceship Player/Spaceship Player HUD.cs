using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;

public class SpaceshipPlayerHUD : MonoBehaviour
{
    [Header("Player HUD")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider boostSlider;
    [SerializeField] private Slider shieldSlider;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI aliveEnemiesText;

    [Header("Flagship Shields")]
    [SerializeField] private Slider playerFlagshipShieldSlider;
    [SerializeField] private Slider enemyFlagshipShieldSlider;
    [SerializeField] private FlagshipController playerFlagship;
    [SerializeField] private FlagshipController enemyFlagship;
    [SerializeField] private int playerFlagshipTeamId = 0;
    [SerializeField] private int enemyFlagshipTeamId = 1;
    [SerializeField] private float flagshipLookupInterval = 0.5f;

    [Header("Font Settings")]
    [SerializeField] private TMP_FontAsset defaultFont; // Anta-Regular SDF
    [SerializeField] private TMP_FontAsset koreanFont;  // NotoSansKR_SDF
    [SerializeField] private TMP_FontAsset chineseFont; // NotoSansSC_SDF

    private GameObject player;
    private SpaceshipHealthComponent playerHealth;
    private SpaceshipMovement playerMovement;

    private FleetSpawner fleetSpawner;
    private float nextFlagshipLookupTime;

    private void Start()
    {
        ResolvePlayerReferences();
        ResolveFlagshipReferences(true);

        if (waveText != null)
            waveText.text = GetTranslatedText("HUD_FLAGSHIP_BATTLE");
            SetFontForText(waveText);

        fleetSpawner = FleetSpawner.Instance;
        if (fleetSpawner != null)
        {
            fleetSpawner.OnAliveEnemiesChanged += SetAliveEnemies;
            SetAliveEnemies(fleetSpawner.AliveTrackedEnemies);
        }
    }

    private void OnDestroy()
    {
        if (fleetSpawner != null)
        {
            fleetSpawner.OnAliveEnemiesChanged -= SetAliveEnemies;
            fleetSpawner = null;
        }
    }

    private void Update()
    {
        ResolvePlayerReferences();
        ResolveFlagshipReferences();

        if (playerHealth != null)
        {
            SetHealth();
            SetShield();
        }

        if (playerMovement != null)
            SetBoost();

        SetFlagshipShield(playerFlagshipShieldSlider, playerFlagship);
        SetFlagshipShield(enemyFlagshipShieldSlider, enemyFlagship);
    }

    public void SetHealth()
    {
        if (healthSlider == null || playerHealth == null) return;

        float health = playerHealth.Health;
        float maxHealth = Mathf.Max(1f, playerHealth.MaxHealth);
        healthSlider.value = health / maxHealth;
    }

    public void SetBoost()
    {
        if (boostSlider == null || playerMovement == null) return;

        float boost = playerMovement.CurrentBoost;
        float maxBoost = Mathf.Max(1f, playerMovement.MaxBoost);
        boostSlider.value = boost / maxBoost;
    }

    public void SetShield()
    {
        if (shieldSlider == null || playerHealth == null) return;

        shieldSlider.value = playerHealth.ShieldRatio;
    }

    private void SetFontForText(TextMeshProUGUI textComponent)
    {
        if (textComponent == null) return;
    
        string localeCode = LocalizationSettings.SelectedLocale.Identifier.Code;
        
        textComponent.font = defaultFont; // Anta-Regular SDF
    }

    public void SetAliveEnemies(int aliveEnemies)
    {
        if (aliveEnemiesText != null)
        {
            string label = GetTranslatedText("HUD_ENEMIES_LEFT");
            aliveEnemiesText.text = label + " " + aliveEnemies.ToString();

            SetFontForText(aliveEnemiesText);
        }
    }

    private string GetTranslatedText(string textKey)
    {
        if (string.IsNullOrEmpty(textKey)) return "";
        return LocalizationSettings.StringDatabase.GetLocalizedString("UI Text", textKey);
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(Locale newLocale)
    {
        if (waveText != null)
        {
            waveText.text = GetTranslatedText("HUD_FLAGSHIP_BATTLE");
            SetFontForText(waveText);
        }
    
        if (fleetSpawner != null)
            SetAliveEnemies(fleetSpawner.AliveTrackedEnemies);
    }

    private void ResolvePlayerReferences()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            playerHealth = null;
            playerMovement = null;
            return;
        }

        if (playerHealth == null)
            playerHealth = player.GetComponent<SpaceshipHealthComponent>();

        if (playerMovement == null)
            playerMovement = player.GetComponent<SpaceshipMovement>();
    }

    private void ResolveFlagshipReferences(bool forceLookup = false)
    {
        if (!forceLookup && Time.unscaledTime < nextFlagshipLookupTime)
            return;

        if (playerFlagship == null)
            playerFlagship = FindFlagshipForTeam(playerFlagshipTeamId);

        if (enemyFlagship == null)
            enemyFlagship = FindFlagshipForTeam(enemyFlagshipTeamId);

        nextFlagshipLookupTime = Time.unscaledTime + Mathf.Max(0.1f, flagshipLookupInterval);
    }

    private FlagshipController FindFlagshipForTeam(int teamId)
    {
        FlagshipController[] flagships = FindObjectsOfType<FlagshipController>(true);
        FlagshipController bestMatch = null;

        for (int i = 0; i < flagships.Length; i++)
        {
            FlagshipController flagship = flagships[i];
            if (flagship == null || !flagship.isActiveAndEnabled)
                continue;

            int flagshipTeamId = -1;
            if (flagship.TeamAgent != null)
            {
                flagshipTeamId = flagship.TeamAgent.TeamId;
            }
            else if (flagship.TryGetComponent(out TeamAgent teamAgent))
            {
                flagshipTeamId = teamAgent.TeamId;
            }

            if (flagshipTeamId != teamId)
                continue;

            if (bestMatch == null ||
                (bestMatch.CurrentState == FlagshipController.BattleState.Destroyed &&
                 flagship.CurrentState != FlagshipController.BattleState.Destroyed))
            {
                bestMatch = flagship;
            }
        }

        return bestMatch;
    }

    private void SetFlagshipShield(Slider slider, FlagshipController flagship)
    {
        if (slider == null) return;

        SpaceshipHealthComponent flagshipHealth = null;
        if (flagship != null)
            flagshipHealth = flagship.Health != null ? flagship.Health : flagship.GetComponent<SpaceshipHealthComponent>();

        slider.value = flagshipHealth != null ? flagshipHealth.ShieldRatio : 0f;
    }
}
