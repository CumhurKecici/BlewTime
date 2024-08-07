using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class LoadingScreenManager : MonoBehaviour
{
    public AsyncOperationHandle loadingHandle;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        loadingHandle = Addressables.LoadSceneAsync(GameManager.Instance.RequestedScene, UnityEngine.SceneManagement.LoadSceneMode.Single, true);
        StartCoroutine(LoadingProgess());
    }

    IEnumerator LoadingProgess()
    {
        while (!loadingHandle.IsDone)
        {
            Debug.Log(loadingHandle.PercentComplete);
            yield return null;
        }
    }
}
