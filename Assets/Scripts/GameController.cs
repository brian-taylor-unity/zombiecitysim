using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    public static GameController instance = null;

    public int numTilesX = 130;
    public int numTilesY = 130;
    public int numStreets = 30;
    public int numHumans = 1000;
    public int numZombies = 1;
    public int humanTurnDelay = 1;
    public int zombieTurnDelay = 3;
    public float turnDelayTime = 0.025f;

    public Slider numHumansSlider;
    public InputField numHumansInputField;
    public Slider numZombiesSlider;
    public InputField numZombiesInputField;
    public InputField numTilesXInputField;
    public InputField numTilesYInputField;
    public InputField streetsInputField;

    public InputField humanTurnDelayInputField;
    public InputField zombieTurnDelayInputField;
    public InputField turnDelayTimeInputField;
    public Slider turnDelayTimeSlider;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    // Use this for initialization
    void Start()
    {
        numTilesXInputField.text = numTilesX.ToString();
        numTilesYInputField.text = numTilesY.ToString();
        streetsInputField.text = numTilesY.ToString();
        numHumansSlider.value = numHumans;
        numHumansInputField.text = numHumans.ToString();
        numZombiesSlider.value = numZombies;
        numZombiesInputField.text = numZombies.ToString();
        humanTurnDelayInputField.text = humanTurnDelay.ToString();
        zombieTurnDelayInputField.text = zombieTurnDelay.ToString();
        turnDelayTimeInputField.text = (turnDelayTime * 1000).ToString();
        turnDelayTimeSlider.value = turnDelayTime * 1000;
    }

    // Update is called once per frame
    void Update()
    {
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

    public void SetNumHumansSlider(float num)
    {
        numHumans = (int)num;
        numHumansInputField.text = numHumans.ToString();
    }

    public void SetNumHumansInputField(string num)
    {
        numHumans = int.Parse(num);
        numHumansSlider.value = numHumans;
    }

    public void SetNumZombiesSlider(float num)
    {
        numZombies = (int)num;
        numZombiesInputField.text = numZombies.ToString();
    }

    public void SetNumZombiesInputField(string num)
    {
        numZombies = int.Parse(num);
        numZombiesSlider.value = numZombies;
    }

    public void SetHumanTurnDelay(string num)
    {
        humanTurnDelay = int.Parse(num);
    }

    public void SetZombieTurnDelay(string num)
    {
        zombieTurnDelay = int.Parse(num);
    }

    public void SetTurnDelayTimeInputField(string num)
    {
        turnDelayTime = float.Parse(num) / 1000;
        turnDelayTimeSlider.value = float.Parse(num);
    }

    public void SetTurnDelayTimeSlider(float num)
    {
        turnDelayTime = num / 1000;
        turnDelayTimeInputField.text = num.ToString();
    }
}
