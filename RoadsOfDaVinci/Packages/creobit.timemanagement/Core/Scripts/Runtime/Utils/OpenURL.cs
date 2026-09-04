using UnityEngine;

public class OpenURL : MonoBehaviour
{
    public void OpenWeb(string url)
    {
        Application.OpenURL(url);
    }
}
