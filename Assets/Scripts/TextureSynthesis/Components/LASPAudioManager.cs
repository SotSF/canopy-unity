using UnityEngine;
using Lasp;
public class LASPAudioManager : MonoBehaviour
{
    public static SpectrumAnalyzer spectrumAnalyzer;
    public static AudioLevelTracker audioLevelTracker;
    public InputStream inputStream;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spectrumAnalyzer = GetComponent<SpectrumAnalyzer>();
        audioLevelTracker = GetComponent<AudioLevelTracker>();
    }

}
