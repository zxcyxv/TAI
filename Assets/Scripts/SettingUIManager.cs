using TMPro;
using UnityEditor;
using UnityEngine;

public class SettingUIManager : MonoBehaviour
{
    [SerializeField] private GameObject settingUI;
    [SerializeField] private TextMeshProUGUI isBotActivated;
    [SerializeField] private TextMeshProUGUI isConnectBot;
    [SerializeField] private TMP_InputField waitTimeInput;
    [SerializeField] private TMP_InputField datasetCountInput;

    // 봇 연결 상태 불러오기
    [SerializeField] private ColdClearAgent agent;
    // 봇 활성화 유무 불러오기
    [SerializeField] private ControlCommandManager ccm;

    private bool settingUIEnabled = false;

    public void BotStart()
    {
        agent.SetActive(true);
        TextIsBotActivated(true);
    }

    public void BotStop()
    {
        agent.SetActive(false);
        TextIsBotActivated(false);
    }

    public void SetWaitTime()
    {
        if (float.TryParse(waitTimeInput.text, out float result)) {
            if (result >= 0f) ccm.hardDropQueueDelay = (float)System.Math.Round(result, 2);
            TextWaitTimeInput(ccm.hardDropQueueDelay);
        }
    }

    public void DataSetSave()
    {
        if (int.TryParse(datasetCountInput.text, out int count))
        {
            DataHandler.Instance?.SetTargetGames(count);
        }
    }

    public void SettingUIEnable(bool enable)
    {
        settingUI.SetActive(enable);
        settingUIEnabled = enable;
        if (enable) Start();
    }

    private void Start()
    {
        if (!settingUIEnabled) return;
        TextIsBotActivated(agent.enableBot);
        TextIsConnectBot(false);
        TextWaitTimeInput(ccm.hardDropQueueDelay);
    }

    private void Update()
    {
        
    }

    private void TextWaitTimeInput(float waitTime)
    {
        waitTimeInput.text = waitTime.ToString();
    }

    private void TextIsBotActivated(bool isActivated)
    {
        if (isActivated) isBotActivated.text = "봇 활성화 됨";
        else isBotActivated.text = "봇 비활성화 됨";
    }


    private void TextIsConnectBot(bool isConnect)
    {
        if (isConnect) isConnectBot.text = "Cold Clear 연결됨";
        else isConnectBot.text = "Cold Clear 연결 안 됨";
    }
}
