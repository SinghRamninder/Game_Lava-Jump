using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string scneneToLoad;
    [SerializeField] private GameObject mainMenuObject;
    [SerializeField] private GameObject creditsMenuObject;
    [SerializeField] private GameObject optionsMenuObject;

    public void Play()
    {
        SceneManager.LoadScene(scneneToLoad);
    }

    public void Credits()
    {
        mainMenuObject.SetActive(false);
        creditsMenuObject.SetActive(true);
    }

    public void Options()
    {
        mainMenuObject.SetActive(false);
        optionsMenuObject.SetActive(true);
    }

    public void Back()
    {
        creditsMenuObject.SetActive(false);
        if (optionsMenuObject != null)
        {
            optionsMenuObject.SetActive(false);
        }
        mainMenuObject.SetActive(true);
    }

    public void Quit()
    {
        Debug.Log("Quit");
        Application.Quit();
    }

}
