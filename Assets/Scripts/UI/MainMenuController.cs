using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private static MainMenuController m_instance;
    public static MainMenuController Instance { get { return m_instance; } }

    [SerializeField] private AssetReference gameplayScene;
    [SerializeField] private AssetReference mainMenuScene;

    [SerializeField] private GameObject endGameScreen;
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private Text resultText;

    void Awake()
    {
        m_instance = this;
    }

    public void StartGame()
    {
        GameManager.Instance.RequestSceneLoad(gameplayScene);
    }

    public void Options()
    {
        Debug.Log("Options");
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void ShowWinScreen()
    {
        GameManager.Instance.State = GameState.Paused;
        endGameScreen.SetActive(true);
        pauseButton.SetActive(false);
        resultText.text = "You Win!";
    }

    public void ShowLoseScreen()
    {
        GameManager.Instance.State = GameState.Paused;
        endGameScreen.SetActive(true);
        pauseButton.SetActive(false);
        resultText.text = "You Lose!";
    }

    public void Pause()
    {
        GameManager.Instance.State = GameState.Paused;
    }

    public void Continue()
    {
        GameManager.Instance.State = GameState.PlayMode;
    }

    public void Restart()
    {
        GameManager.Instance.RequestSceneLoad(gameplayScene);
    }

    public void EndGame()
    {
        GameManager.Instance.RequestSceneLoad(mainMenuScene);
    }
}
