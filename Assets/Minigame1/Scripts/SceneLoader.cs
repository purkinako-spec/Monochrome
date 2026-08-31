using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private string sceneName = "FishingMinigameScene";

    public void LoadScene()
    {
        SceneManager.LoadSceneAsync(
            sceneName,
            LoadSceneMode.Additive
        );
    }

    public void UnloadScene()
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);

        if (scene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}