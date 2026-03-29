using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopUpReplay : MonoBehaviour
{
    public static PopUpReplay Instance;
    public BlocksManager BlocksManager;
    [SerializeField]
    private Button _btnReplay;

    [SerializeField]
    private RectTransform _cantPlace;
    [SerializeField]
    private GameObject _content;
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
        _cantPlace.DOKill();

        _cantPlace.transform.localScale = Vector3.one * 0.5f;

        _cantPlace.gameObject.SetActive(true);

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.Append(_cantPlace.transform.DOScale(1, 0.25f).SetEase(Ease.OutBack));

        seq.AppendInterval(1f);
        seq.OnComplete(() =>
        {
            _cantPlace.gameObject.SetActive(false);
            _content.SetActive(true);

        });
    }
    public void Hide()
    {
        Time.timeScale = 1;
        _content.SetActive(false);
    }
}
