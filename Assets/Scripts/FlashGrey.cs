using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FlashGrey : MonoBehaviour
{
    [SerializeField] private Image flashPanel;
    [SerializeField] private float flashDuration = 0.9f; // seconds

    public void SetPanel(Image img) => flashPanel = img;

    public IEnumerator DoFlash()
    {
        if (flashPanel == null) yield break;
        flashPanel.gameObject.SetActive(true);   // instant ON
        yield return new WaitForSeconds(flashDuration);
        flashPanel.gameObject.SetActive(false);  // instant OFF
    }
}
