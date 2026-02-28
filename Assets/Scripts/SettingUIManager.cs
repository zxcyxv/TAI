using TMPro;
using UnityEditor;
using UnityEngine;

public class SettingUIManager : MonoBehaviour
{
    [SerializeField] private GameObject settingUI;
    [SerializeField] private TextMeshProUGUI isBotActivated;
    [SerializeField] private TextMeshProUGUI isRecording;
    [SerializeField] private TextMeshProUGUI isConnectBot;
    [SerializeField] private TMP_InputField waitTimeInput;

    [SerializeField] private ColdClearAgent agent;
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
            if (result >= 0.1f) ccm.hardDropQueueDelay = (float)System.Math.Round(result, 2);
            TextWaitTimeInput(ccm.hardDropQueueDelay);
        }
    }

    public void RecordStart()
    {
        TextIsRecording(true);
    }

    public void RecordStop()
    {
        TextIsRecording(false);
    }

    public void DataSetSave()
    {
        
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
        TextIsRecording(false);
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

    private void TextIsRecording(bool isRec)
    {
        if (isRec) isRecording.text = "기록 저장 중 O";
        else isRecording.text = "기록 저장 중 X";
    }

    private void TextIsConnectBot(bool isConnect)
    {
        if (isConnect) isConnectBot.text = "Cold Clear 연결됨";
        else isConnectBot.text = "Cold Clear 연결 안 됨";
    }
}
