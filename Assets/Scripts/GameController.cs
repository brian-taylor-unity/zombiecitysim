using System.Globalization;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    public static GameController Instance;

    public int numTilesX = 130;
    public int numTilesY = 130;
    public int numStreets = 30;

    public int numHumans = 1000;
    public int humanStartingHealth = 100;
    public int humanDamage = 0;
    public int humanVisionDistance = 10;
    public int humanTurnDelay = 1;

    public int numZombies = 1;
    public int zombieStartingHealth = 70;
    public int zombieDamage = 20;
    public int zombieVisionDistance = 6;
    public int zombieHearingDistance = 10;
    public int zombieTurnDelay = 3;

    public int audibleDecayTime = 20;

    public float turnDelayTime = 0.025f;

    public InputField numHumansInputField;
    public Slider numHumansSlider;
    public InputField numZombiesInputField;
    public Slider numZombiesSlider;

    public InputField numTilesXInputField;
    public InputField numTilesYInputField;
    public InputField streetsInputField;

    public InputField humanTurnDelayInputField;
    public InputField zombieTurnDelayInputField;
    public InputField turnDelayTimeInputField;
    public Slider turnDelayTimeSlider;

    public bool mouseBlockedByUI;
    private int _uiElementsBlocked;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _uiElementsBlocked = 0;
    }

    // Use this for initialization
    void Start()
    {
        numHumansInputField.text = numHumans.ToString();
        numHumansSlider.value = numHumans;
        numZombiesInputField.text = numZombies.ToString();
        numZombiesSlider.value = numZombies;

        numTilesXInputField.text = numTilesX.ToString();
        numTilesYInputField.text = numTilesY.ToString();
        streetsInputField.text = numStreets.ToString();

        humanTurnDelayInputField.text = humanTurnDelay.ToString();
        zombieTurnDelayInputField.text = zombieTurnDelay.ToString();
        turnDelayTimeInputField.text = (turnDelayTime * 1000).ToString(CultureInfo.InvariantCulture);
        turnDelayTimeSlider.value = turnDelayTime * 1000;

        CreateSpawnWorldComponentEntity();
    }

    private void CreateUpdateGameControllerComponentEntity()
    {
        World.DefaultGameObjectInjectionWorld.EntityManager.CreateSingleton<UpdateGameControllerComponent>();
    }

    private void CreateSpawnWorldComponentEntity()
    {
        World.DefaultGameObjectInjectionWorld.EntityManager.CreateSingleton<SpawnWorld>();
    }

    public void SetNumHumansInputField(string num)
    {
        numHumans = int.Parse(num);
        numHumansSlider.value = numHumans;
    }

    public void SetNumHumansSlider(float num)
    {
        numHumans = (int)num;
        numHumansInputField.text = numHumans.ToString();
    }

    public void SetNumZombiesInputField(string num)
    {
        numZombies = int.Parse(num);
        numZombiesSlider.value = numZombies;
    }

    public void SetNumZombiesSlider(float num)
    {
        numZombies = (int)num;
        numZombiesInputField.text = numZombies.ToString();
    }

    public void SetNumTilesXInputField(string num)
    {
        numTilesX = int.Parse(num);
    }

    public void SetNumTilesYInputField(string num)
    {
        numTilesY = int.Parse(num);
    }

    public void SetNumStreetsInputField(string num)
    {
        numStreets = int.Parse(num);
    }

    public void OnRegeneratePressed()
    {
        CreateUpdateGameControllerComponentEntity();
        CreateSpawnWorldComponentEntity();
    }

    public void SetHumanTurnDelay(string num)
    {
        humanTurnDelay = int.Parse(num);

        CreateUpdateGameControllerComponentEntity();
    }

    public void SetZombieTurnDelay(string num)
    {
        zombieTurnDelay = int.Parse(num);

        CreateUpdateGameControllerComponentEntity();
    }

    public void SetTurnDelayTimeInputField(string num)
    {
        turnDelayTime = float.Parse(num) / 1000;
        turnDelayTimeSlider.value = float.Parse(num);

        CreateUpdateGameControllerComponentEntity();
    }

    public void SetTurnDelayTimeSlider(float num)
    {
        turnDelayTime = num / 1000;
        turnDelayTimeInputField.text = num.ToString(CultureInfo.InvariantCulture);

        CreateUpdateGameControllerComponentEntity();
    }

    public void EnterBlockingUI()
    {
        mouseBlockedByUI = true;
        _uiElementsBlocked++;
    }

    public void ExitBlockingUI()
    {
        _uiElementsBlocked--;
        if (_uiElementsBlocked == 0)
            mouseBlockedByUI = false;
    }
}
