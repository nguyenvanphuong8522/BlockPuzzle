using UnityEngine;
using UnityEngine.UI;

public class PopUpReplay : MonoBehaviour
{
    public static PopUpReplay Instance;
    public BlocksManager BlocksManager;
    [SerializeField]
    private Button _btnReplay;

    private void Awake()
    {
        Instance = this;
        _btnReplay.onClick.AddListener(OnReplayClick);
        Hide();
    }

    private void OnReplayClick()
    {
        Hide();
        Board.Instance.ResetData();
        BlocksManager.RefreshListBlocks();

    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
} 
