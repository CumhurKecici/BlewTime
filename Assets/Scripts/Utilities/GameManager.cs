using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class GameManager : MonoBehaviour
{
    private static GameManager m_instance;
    public static GameManager Instance { get { return m_instance; } }

    [SerializeField] private AssetReference m_loadingScene;
    [SerializeField] private AssetReference m_mainMenuScene;
    [SerializeField] private AssetReference m_requestedScene;

    public AssetReference RequestedScene { get { return m_requestedScene; } }

    public List<BombermanUnit> BombermanUnits;
    public GameState State = GameState.OnMenu;
    public bool IsPlayMode { get { return State == GameState.PlayMode ? true : false; } }


    void Awake()
    {
        if (m_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_instance = this;
        DontDestroyOnLoad(this);
    }

    private void LoadingScreen()
    {
        Addressables.LoadSceneAsync(m_loadingScene, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
    }

    public void RequestSceneLoad(AssetReference scene)
    {
        m_requestedScene = scene;
        LoadingScreen();
    }

    public void GoToMainMenu()
    {
        Addressables.LoadSceneAsync(m_mainMenuScene, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
    }



}

public enum GameState
{
    PlayMode,
    Paused,
    OnMenu
}
