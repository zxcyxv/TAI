using TMPro;
using UnityEngine;

public enum Spin
{
    None,
    TSpin,
    LSpin,
    JSpin,
    ISpin,
    SSpin,
    ZSpin
}

public class Scoreboard : MonoBehaviour
{
    [SerializeField] private Board board;
    [SerializeField] private TextMeshProUGUI btbText; 
    [SerializeField] private TextMeshProUGUI scoreText; 
    [SerializeField] private TextMeshProUGUI spinText; 
    [SerializeField] private TextMeshProUGUI comboText; 

    public void ChangeBTBText(int btb)
    {
        if (btb == 0)
        {
            btbText.enabled = false;
        } 
        else
        {
            btbText.text = "BTB x" + btb.ToString();
            btbText.enabled = true;
        }
        
    }

    public void ChangeScoreText(int score)
    {
        scoreText.text = "Score: " + score.ToString();
    }

    public void ChangeSpinText(Spin spin)
    {
        if (spin == Spin.None)
        {
            spinText.enabled = false;
            return;
        }
        string defaultText = "Spin: ";
        defaultText += spin.ToString();
        spinText.text = defaultText;
        spinText.enabled = true;
    }

    public void ChangeComboText(int combo)
    {
        if (combo <= 0)
        {
            comboText.enabled = false;
            return;
        }
        string defaultText = "Combo: ";
        defaultText += combo.ToString();
        comboText.text = defaultText;
        comboText.enabled = true;
    }

    private void Update()
    {
        ChangeBTBText(board.BackToBackChain);
        ChangeScoreText(board.Score);
        ChangeSpinText(board.LastLockSpin);
        ChangeComboText(board.Combo);
    } 
}
