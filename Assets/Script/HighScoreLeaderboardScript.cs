using System.Collections;
using Dan.Main;
using Dan.Models;
using TMPro;
using UnityEditor.Build.Content;
using UnityEngine;

namespace Dan.Demo
{
    public class HighScoreLeaderboardScript : MonoBehaviour
    {
        [Header("Gameplay:")]
        [SerializeField] private TextMeshProUGUI _playerScoreText;
        
        [Header("Leaderboard Essentials:")]
        [SerializeField] private TMP_InputField _playerUsernameInput;
        [SerializeField] private Transform _entryDisplayParent;
        [SerializeField] private EntryDisplay _entryDisplayPrefab;
        [SerializeField] private CanvasGroup _leaderboardLoadingPanel;

        [Header("Search Query Essentials:")]
        [SerializeField] private TMP_Dropdown _timePeriodDropdown;
        [SerializeField] private TMP_InputField _pageInput, _entriesToTakeInput;
        [SerializeField] private int _defaultPageNumber = 1, _defaultEntriesToTake = 100;

        [Header("Personal Entry:")]
        [SerializeField] private RectTransform _personalEntryPanel;
        [SerializeField] private TextMeshProUGUI _personalEntryText;

        [Header("Submit Panel: ")]
        [SerializeField] private GameObject _submitPanel;

        private Coroutine _personalEntryMoveCoroutine;

        public void UpdatePlayerScore()
        {
            _playerScoreText.text = $"Your score: {GameManager.score}";
        }
        
        public void Load()
        {
            var timePeriod = 
                _timePeriodDropdown.value == 1 ? Dan.Enums.TimePeriodType.Today :
                _timePeriodDropdown.value == 2 ? Dan.Enums.TimePeriodType.ThisWeek :
                _timePeriodDropdown.value == 3 ? Dan.Enums.TimePeriodType.ThisMonth :
                _timePeriodDropdown.value == 4 ? Dan.Enums.TimePeriodType.ThisYear : Dan.Enums.TimePeriodType.AllTime;

            var pageNumber = int.TryParse(_pageInput.text, out var pageValue) ? pageValue : _defaultPageNumber;
            pageNumber = Mathf.Max(1, pageNumber);
            _pageInput.text = pageNumber.ToString();
            
            var take = int.TryParse(_entriesToTakeInput.text, out var takeValue) ? takeValue : _defaultEntriesToTake;
            take = Mathf.Clamp(take, 1, 100);
            _entriesToTakeInput.text = take.ToString();
            
            var searchQuery = new LeaderboardSearchQuery
            {
                Skip = (pageNumber - 1) * take,
                Take = take,
                TimePeriod = timePeriod
            };
            
            _pageInput.image.color = Color.white;
            _entriesToTakeInput.image.color = Color.white;
            
            Leaderboards.HighScoreLeaderboard.GetEntries(searchQuery, OnLeaderboardLoaded, ErrorCallback);
            ToggleLoadingPanel(true);
        }

        public void ChangePageBy(int amount)
        {
            var pageNumber = int.TryParse(_pageInput.text, out var pageValue) ? pageValue : _defaultPageNumber;
            pageNumber += amount;
            if (pageNumber < 1) return;
            _pageInput.text = pageNumber.ToString();
            // After the user change page, load immediately
            Load();
        }
        
        private void OnLeaderboardLoaded(Entry[] entries)
        {
            foreach (Transform t in _entryDisplayParent) 
                Destroy(t.gameObject);

            foreach (var t in entries) 
                CreateEntryDisplay(t);
            
            ToggleLoadingPanel(false);
        }
        
        private void ToggleLoadingPanel(bool isOn)
        {
            _leaderboardLoadingPanel.alpha = isOn ? 1f : 0f;
            _leaderboardLoadingPanel.interactable = isOn;
            _leaderboardLoadingPanel.blocksRaycasts = isOn;
        }

        public void MovePersonalEntryMenu(float xPos)
        {
            if (_personalEntryMoveCoroutine != null) 
                StopCoroutine(_personalEntryMoveCoroutine);
            _personalEntryMoveCoroutine = StartCoroutine(MoveMenuCoroutine(_personalEntryPanel, 
                new Vector2(xPos, _personalEntryPanel.anchoredPosition.y)));
        }

        private IEnumerator MoveMenuCoroutine(RectTransform rectTransform, Vector2 anchoredPosition)
        {
            const float duration = 0.25f;
            var time = 0f;
            var startPosition = rectTransform.anchoredPosition;
            while (time < duration)
            {
                time += Time.deltaTime;
                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, anchoredPosition, time / duration);
                yield return null;
            }
            
            rectTransform.anchoredPosition = anchoredPosition;
            _personalEntryMoveCoroutine = null;
        }
        
        private void CreateEntryDisplay(Entry entry)
        {
            var entryDisplay = Instantiate(_entryDisplayPrefab.gameObject, _entryDisplayParent);
            entryDisplay.GetComponent<EntryDisplay>().SetEntry(entry);
        }

        private IEnumerator LoadingTextCoroutine(TMP_Text text)
        {
            var loadingText = "Loading";
            for (int i = 0; i < 3; i++)
            {
                loadingText += ".";
                text.text = loadingText;
                yield return new WaitForSeconds(0.25f);
            }
            
            StartCoroutine(LoadingTextCoroutine(text));
        }

        private void InitializeComponents()
        {
            StartCoroutine(LoadingTextCoroutine(_leaderboardLoadingPanel.GetComponentInChildren<TextMeshProUGUI>()));

            _pageInput.onValueChanged.AddListener(_ => _pageInput.image.color = Color.yellow);
            _entriesToTakeInput.onValueChanged.AddListener(_ => _entriesToTakeInput.image.color = Color.yellow);

            _pageInput.placeholder.GetComponent<TextMeshProUGUI>().text = _defaultPageNumber.ToString();
            _entriesToTakeInput.placeholder.GetComponent<TextMeshProUGUI>().text = _defaultEntriesToTake.ToString();

            UpdatePlayerScore();
        }
        
        private void Start()
        {
            InitializeComponents();
            
            Load();

        }

        public void Submit()
        {
            // Only can submit if string is submittable
            // A string is submittable if it contains no space and no symbols except _
            char[] allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_".ToCharArray();
            if (ContainsOnlyAllowedCharacters(_playerUsernameInput.text, allowedChars))
            {
                // And is not longer than 16 characters
                if (_playerUsernameInput.text.Length <= 16)
                {
                    // And cannot be null
                    if (_playerUsernameInput.text.Length != 0)
                    {
                        // Valid submission
                        Leaderboards.HighScoreLeaderboard.UploadNewEntry(_playerUsernameInput.text, GameManager.score, Callback, ErrorCallback);
                        CloseSubmitPanel();
                    }
                    else // User input empty string
                    {
                        Debug.Log("Cannot enter empty string");
                    }
                    
                }
                else
                // Longer than 16 characters
                {
                    Debug.Log("Entry cannot be longer than 16 characters");
                }
            } else
            // Contains space or symbols
            {
                Debug.Log("Entry can't contain spaces and symbols (except _)");
            }
            
        }
        
        public void DeleteEntry()
        {
            Leaderboards.HighScoreLeaderboard.DeleteEntry(Callback, ErrorCallback);
        }

        public void ResetPlayer()
        {
            LeaderboardCreator.ResetPlayer();
        }

        public void GetPersonalEntry()
        {
            Leaderboards.HighScoreLeaderboard.GetPersonalEntry(OnPersonalEntryLoaded, ErrorCallback);
        }

        private void OnPersonalEntryLoaded(Entry entry)
        {
            _personalEntryText.text = $"{entry.RankSuffix()}. {entry.Username} : {entry.Score}";
            MovePersonalEntryMenu(0f);
        }
        
        private void Callback(bool success)
        {
            if (success)
                Load();
        }
        
        private void ErrorCallback(string error)
        {
            Debug.LogError(error);
        }

        private bool ContainsOnlyAllowedCharacters(string text, char[] allowed)
        {
            foreach (char c in text)
            {
                bool isAllowed = false;
                foreach (char a in allowed)
                {
                    if (c == a)
                    {
                        isAllowed = true;
                        break; // Character is allowed, move to the next character in the text
                    }
                }
                if (!isAllowed)
                {
                    return false; // Found a character that is NOT allowed
                }
            }
            return true; // All characters in the text are allowed
        }

        public void ToggleSubmitPanel()
        {
            _submitPanel.SetActive(!_submitPanel.activeSelf);
        }

        private void CloseSubmitPanel()
        {
            _submitPanel.SetActive(false);
        }
    }
}
