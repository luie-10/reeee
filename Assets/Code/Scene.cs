using UnityEngine;
using UnityEngine.SceneManagement;
public class Scene : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    public void SceneMove(string SceneName)
    {
        SceneManager.LoadScene(SceneName);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
