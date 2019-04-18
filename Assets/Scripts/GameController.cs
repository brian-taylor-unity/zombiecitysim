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
    public float turnDelayTime = 1.0f;

    public Slider numHumansSlider;
    public InputField numHumansInputField;
    public Slider numZombiesSlider;
    public InputField numZombiesInputField;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    // Use this for initialization
    void Start()
    {
        numHumansSlider.value = numHumans;
        numHumansInputField.text = numHumans.ToString();
        numZombiesSlider.value = numZombies;
        numZombiesInputField.text = numZombies.ToString();
    }

    // Update is called once per frame
    void Update()
    {
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
}
