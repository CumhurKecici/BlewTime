using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TimerController : MonoBehaviour
{
    private Text timerText;

    [SerializeField] private int minutes = 5;
    [SerializeField] private int seconds = 1;
    [SerializeField] private GameObject endGameScreen;
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private Text resultText;

    private bool isTimedUp = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        timerText = GetComponent<Text>();
        StartCoroutine(Timer());
    }

    void Update()
    {
        timerText.text = "0" + minutes + ":" + (seconds.ToString().Length == 2 ? seconds : "0" + seconds);
        if (isTimedUp)
            ShowResults();
        //GameManager.Instance.GoToMainMenu();


    }

    private void ShowResults()
    {
        GameManager.Instance.State = GameState.Paused;
        endGameScreen.SetActive(true);
        pauseButton.SetActive(false);
        resultText.text = "Time Up!";
    }

    IEnumerator Timer()
    {
        while (!isTimedUp)
        {
            if (GameManager.Instance.IsPlayMode)
                seconds--;

            if (seconds == 0 && minutes == 0)
                isTimedUp = true;

            if (seconds < 0 && minutes > 0)
            {
                seconds = 59;
                minutes--;
            }

            yield return new WaitForSeconds(1.0f);
        }
    }
}
