
using JetBrains.Annotations;
using System;
using Thry.Udon.PrivateRoom;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Thry.YTDB
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class YT_Tablet : UdonSharpBehaviour
    {
        const int PLAYLIST_AUTO_GEN_LENGTH = 10;
        const int MAX_PLAYLIST_LENGTH = 100;
        const int SONGS_PER_LOAD_STEP = 20;
        const int ARTISTS_PER_LOAD_STEP = 5;

        [Header("On Setup")]
        public YT_DB Database;
        public UdonBehaviour Adapter;

        [Space(50)]

        public string InitialSong = "\"Slut!";
        public Animator LoadBarAnimator;
        public Animator PlaylistAnimator;
        public YT_Card CardPrefab;
        public YT_SongButton SongButtonPrefab;
        public Transform SongsContainer;
        public Transform ArtistsContainer;
        public GameObject ButtonSongsShowMore;
        public GameObject ButtonArtistsShowMore;
        public VRC.SDK3.Components.VRCUrlInputField SearchField;
        public Text SearchFieldOverwritePlaceholder;
        public Text SearchFieldOverwriteText;
        public VRC.SDK3.Components.VRCUrlInputField CustomUrlField;
        public UnityEngine.UI.Slider VolumeSlider;
        public Image LogoIcon;
        public Color InControlColor;

        public ScrollRect ScrollbarSearch;
        public ScrollRect ScrollbarPlaylist;

        public YT_ListItem ListItemPrefab;

        public Image PlayImage;
        public Image PauseImage;

        public Transform PlaylistContainer;

        public Image VolumeIcon;
        public Sprite[] VolumeSprites;

        [HideInInspector]
        [UdonSynced] public VRCUrl VideoUrl;

        [UdonSynced] string _searchTerm = "";
        [UdonSynced] bool _isSongNameSearch = true;
        [UdonSynced] int[] _playlist = new int[MAX_PLAYLIST_LENGTH];
        [UdonSynced] bool[] _playlistIsUserRequest = new bool[MAX_PLAYLIST_LENGTH];
        [UdonSynced] string[] _playlistRequestedBy = new string[MAX_PLAYLIST_LENGTH];
        [UdonSynced] VRCUrl[] _playlistCustomURL = new VRCUrl[MAX_PLAYLIST_LENGTH];
        [UdonSynced] int _playlistLength = 0;
        [UdonSynced] int[] _previousSongs = new int[MAX_PLAYLIST_LENGTH];
        [UdonSynced] VRCUrl[] _previousUrls = new VRCUrl[MAX_PLAYLIST_LENGTH];
        [UdonSynced] int _previousSongsHead = 0;
        [UdonSynced] int _previousSongsTail = 0;
        int[] _localPlaylistIndex = new int[MAX_PLAYLIST_LENGTH];
        int _localPlaylistLength = 0;

        [UdonSynced] bool _isPlaylistOpen = false;

        [UdonSynced] float _scrollPlaylistAbsolute = 0.0f;
        [UdonSynced] float _scrollSearchAbsolute = 0.0f;

        float _lastPlaylistScrollHeight = 0;
        float _lastSearchScrollHeight = 0;
        
        bool _supressFirstPlaylistScroll = true;
        bool _supressFirstSearchScroll = true;

        bool _supressScrollPlaylist = false;
        bool _supressScrollSearch = false;

        //bool _supressPlaylistScrollCallback = false;
        //bool _supressPlaylistSearchCallback = false;

        [UdonSynced] int _search_songs_loadSteps = 0;
        int _local_search_song_loadSteps = 0;
        [UdonSynced] int _search_artists_loadSteps = 0;
        int _local_search_artists_loadSteps = 0;

        int[] _resultsSongs = new int[]{ 0, 0, 0};
        int[] _resultsArtists = new int[]{ 0, 0, 0};
        int _songsOffset = 0;
        int _artistsOffset = 0;
        bool _isPlaying = false;
        UdonBehaviour _self;
        YT_SongButton[] _playlistButtons = new YT_SongButton[MAX_PLAYLIST_LENGTH];
        int _skipCallCount = 0;

        float _volume;
        float _preMuteVolume;

        private void Start() 
        {
            _self = this.GetComponent<UdonBehaviour>();
            bool isOwner = Networking.IsOwner(gameObject);
            LogoIcon.color = isOwner ? InControlColor : Color.white;
            if (isOwner && !string.IsNullOrWhiteSpace(InitialSong))
            {
                SendCustomEventDelayedFrames(nameof(CreateInitialQueue), 1);
            }
            for(int i = 0; i < MAX_PLAYLIST_LENGTH; i++)
            {
                _playlistRequestedBy[i] = ""; // Initialize to empty string
                _playlistButtons[i] = Instantiate(SongButtonPrefab.gameObject, PlaylistContainer).GetComponent<YT_SongButton>();
                _playlistButtons[i].gameObject.SetActive(false);
                _localPlaylistIndex[i] = -1;
                _playlistCustomURL[i] = VRCUrl.Empty;

                _previousUrls[i] = VRCUrl.Empty;
            }
            ArtistsContainer.parent.gameObject.SetActive(false);
            SongsContainer.parent.gameObject.SetActive(false);
            ClearSearchObjects();
            UpdateVolumeIcon();
        }

        public void CreateInitialQueue()
        {
            // Search for the initial song
            int[] ids = Database.SearchByName(InitialSong);
            if (ids[0] > 0)
            {
                PlayAndStartNewPlaylist(ids[1]);
                TogglePlaylist();
            }
        }


        [PublicAPI]
        public void TogglePlaylist()
        {
            Debug.Log("[YT Tablet] TogglePlaylist");
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _isPlaylistOpen = !_isPlaylistOpen;
            UpdatePlaylistAnimator();
            RequestSerialization();
        }

        [PublicAPI]
        public void ForceVideoSync()
        {
            Adapter.SendCustomEvent("ForceVideoSync");
        }

        [PublicAPI]
        public void TakeControl()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            Adapter.SendCustomEvent("TakeControl");
        }

        // ===================== UI Callbacks =====================

        public void OnSearchChanged()
        {
            SetSearchField(SearchField.GetUrl().ToString());
        }

        public void OnSearchSubmit()
        {
            Debug.Log("[YT Tablet] OnInputChanged");
            if (_searchTerm == SearchField.GetUrl().ToString())
            {
                return;
            }

            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            if(SearchField.GetUrl().ToString().StartsWith("http"))
            {
                // Add video to queue
                Debug.Log("[YT Tablet] OnCustomUrlAdded");
                VRCUrl newUrl = SearchField.GetUrl();
                if (newUrl != null && newUrl != VideoUrl)
                {
                    EnqueueCustom(newUrl);
                }
            }
            else
            {
                // Search
                _searchTerm = SearchField.GetUrl().ToString();
                SetSearchField();

                _isPlaylistOpen = false;
                UpdatePlaylistAnimator();

                RequestSerialization();
                ExecuteSearch();
            }
            
        }

        private void SetSearchField()
        {
            SetSearchField(_searchTerm);
        }
        
        private void SetSearchField(string s)
        {
            SearchFieldOverwritePlaceholder.enabled = s == "";
            SearchFieldOverwriteText.enabled = s != "";
            SearchFieldOverwriteText.text = s;
        }

        private string GetLocalSearchTerm()
        {
            return SearchFieldOverwriteText.text;
        }

        public void OnCustomUrlAdded()
        {
            Debug.Log("[YT Tablet] OnCustomUrlAdded");
            VRCUrl newUrl = CustomUrlField.GetUrl();
            if (newUrl != null && newUrl.Get() != "" && newUrl != VideoUrl)
            {
                ReplacePlaylistHeadWithCustom(newUrl);
            }
        }

        float ScrollbarNormalizedToAbsolute(ScrollRect bar)
        {
            return (1 - bar.verticalNormalizedPosition) * (bar.content.rect.height - bar.viewport.rect.height);
        }

        float ScrollbarAbsoluteToNormalized(float absolute, ScrollRect bar)
        {
            return 1 - absolute / (bar.content.rect.height - bar.viewport.rect.height);
        }

        public void OnSearchScrollbarChanged()
        {
            if(_supressFirstSearchScroll || _supressFirstSearchScroll)
            {
                _supressFirstSearchScroll = false;
                return;
            }

            float height = ScrollbarSearch.content.rect.height;
            bool heightChanged = _lastSearchScrollHeight != height;
            if (heightChanged)
            {
                Debug.Log($"[YT Tablet] OnSearchScrollbarChanged Height Changed {_lastSearchScrollHeight} {height}");
                _lastSearchScrollHeight = height;
                ScrollbarSearch.verticalNormalizedPosition = ScrollbarAbsoluteToNormalized(_scrollSearchAbsolute, ScrollbarSearch);
                return;
            }
            float currentAbsolute = ScrollbarNormalizedToAbsolute(ScrollbarSearch);
            bool wasUserInput = Math.Abs(_scrollSearchAbsolute - currentAbsolute) > 0.1f;
            // scrollOutsideOfPossible: this happens when the search got shortend e.g. through joining a room with a long search list
            bool scrollOutsideOfPossible = _scrollSearchAbsolute > height - ScrollbarSearch.viewport.rect.height;
            Debug.Log($"[YT Tablet] OnSearchScrollbarChanged {wasUserInput} {!scrollOutsideOfPossible} {_scrollSearchAbsolute} {currentAbsolute}");

            if (wasUserInput && !scrollOutsideOfPossible && Networking.LocalPlayer != null)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                _scrollSearchAbsolute = currentAbsolute;
                RequestSerialization();
            }
        }

        public void OnPlaylistScrollbarChanged()
        {
            if(_supressFirstPlaylistScroll || _supressScrollPlaylist)
            {
                _supressFirstPlaylistScroll = false;
                return;
            }

            float height = ScrollbarPlaylist.content.rect.height;
            bool heightChanged = _lastPlaylistScrollHeight != height;
            if (heightChanged)
            {
                Debug.Log($"[YT Tablet] OnPlaylistScrollbarChanged Height Changed {_lastPlaylistScrollHeight} {height}");
                _lastPlaylistScrollHeight = height;
                ScrollbarPlaylist.verticalNormalizedPosition = ScrollbarAbsoluteToNormalized(_scrollPlaylistAbsolute, ScrollbarPlaylist);
                return;
            }
            float currentAbsolute = ScrollbarNormalizedToAbsolute(ScrollbarPlaylist);
            bool wasUserInput = Math.Abs(_scrollPlaylistAbsolute - currentAbsolute) > 0.1f;
            // scrollOutsideOfPossible: this happens when the playlist got shortend through a video being removed
            bool scrollOutsideOfPossible = _scrollPlaylistAbsolute > height - ScrollbarPlaylist.viewport.rect.height; 
            Debug.Log($"[YT Tablet] OnPlaylistScrollbarChanged {wasUserInput} {!scrollOutsideOfPossible} {_scrollPlaylistAbsolute} {currentAbsolute}");

            if (wasUserInput && !scrollOutsideOfPossible && Networking.LocalPlayer != null)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                _scrollPlaylistAbsolute = currentAbsolute;
                RequestSerialization();
            }
        }

        [PublicAPI]
        public void SetVolume(float newVolume, bool updateVideoplayer)
        {
            if (_volume == newVolume) return;
            _volume = newVolume;
            
            VolumeSlider.SetValueWithoutNotify(_volume);
            UpdateVolumeIcon();

            if (updateVideoplayer)
                Adapter.SendCustomEvent("UpdateVolumeFromTablet");
        }

        [PublicAPI]
        public float GetVolume()
        {
            return _volume;
        }

        public void OnVolumeChange()
        {
            float newVolume = VolumeSlider.value;
            if (_volume > 0 && newVolume == 0)
            {
                _preMuteVolume = 0.5f;
            }
            SetVolume(newVolume, true);
        }

        public void ToggleMute()
        {
            bool isMuted = _volume == 0f;
            isMuted = !isMuted;

            if (isMuted)
            {
                _preMuteVolume = _volume;
                SetVolume(0, true);
            }
            else
            {
                SetVolume(_preMuteVolume, true);
            }
        }

        public void LoadMoreSongs()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _search_songs_loadSteps++;
            ShowMoreSongs();
            RequestSerialization();
        }

        public void LoadMoreArtists()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _search_artists_loadSteps++;
            ShowMoreArtists();
            RequestSerialization();
        }

        // ===================== Networking =====================

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Debug.Log("[YT Tablet] New Owner: " + player.displayName);
            LogoIcon.color = player.isLocal ? InControlColor : Color.white;
        }

        public override void OnDeserialization()
        {
            Debug.Log("[YT Tablet] OnDeserialization");
            // sync scrollbars
            UpdateScrollbars();
            // sync playlist
            UpdatePlaylist();
            // sync scrollbars
            UpdateScrollbars();
            if (_searchTerm != GetLocalSearchTerm())
            {
                // Update search term
                SetSearchField();
                ExecuteSearch();
            }
            else
            {
                // Sync search result length
                ShowMoreSongs();
                ShowMoreArtists();
            }

        }


        // ===================== Internals =====================

        void UpdateVolumeIcon()
        {
            if (_volume == 0)
                VolumeIcon.sprite = VolumeSprites[0];
            else if (_volume < 0.1f)
                VolumeIcon.sprite = VolumeSprites[1];
            else if (_volume < 0.5f)
                VolumeIcon.sprite = VolumeSprites[2];
            else
                VolumeIcon.sprite = VolumeSprites[3];
        }

        void UpdatePlaylistAnimator()
        {
            PlaylistAnimator.SetBool("IsOpen", _isPlaylistOpen);
        }
        
        void UpdateScrollbars()
        {
            Debug.Log("[YT Tablet] UpdateScrollbars");
            _supressScrollPlaylist = true;
            _supressScrollPlaylist = true;
            ScrollbarSearch.verticalNormalizedPosition = ScrollbarAbsoluteToNormalized(_scrollSearchAbsolute, ScrollbarSearch);
            ScrollbarPlaylist.verticalNormalizedPosition = ScrollbarAbsoluteToNormalized(_scrollPlaylistAbsolute, ScrollbarPlaylist);
            _supressScrollPlaylist = false;
            _supressScrollPlaylist = false;
        }

        void UpdatePlaylist()
        {
            Debug.Log("[YT Tablet] UpdatePlaylist");
            UpdatePlaylistAnimator();
            // enable / disable buttons
            if (_localPlaylistLength != _playlistLength)
            {
                for (int i = _playlistLength; i < _localPlaylistLength; i++)
                {
                    _playlistButtons[i].gameObject.SetActive(false);
                }
                for (int i = _localPlaylistLength; i < _playlistLength; i++)
                {
                    _playlistButtons[i].gameObject.SetActive(true);
                }
                _localPlaylistLength = _playlistLength;
                _scrollPlaylistAbsolute = ScrollbarNormalizedToAbsolute(ScrollbarPlaylist);
            }
            // update values
            for (int i = 0; i < _playlistLength; i++)
            {
                if (_localPlaylistIndex[i] != _playlist[i] || (_playlist[i] == -1))
                {
                    _localPlaylistIndex[i] = _playlist[i];
                    if (_playlist[i] == -1)
                    {
                        string name = _playlistCustomURL[i].Get();
                        string artist = "Unknown";
                        if (name.Contains("youtube.com/watch?v="))
                        {
                            name = name.Split('=')[1].Split('&')[0];
                            artist = "YouTube";
                        }
                        _playlistButtons[i].Setup(this, -1, name, artist, _playlistRequestedBy[i], 0, i);
                    }
                    else
                    {
                        int songIndex = _playlist[i];
                        _playlistButtons[i].Setup(this, songIndex, Database.GetSongName(songIndex), Database.GetSongArtist(songIndex), _playlistRequestedBy[i], Database.GetSongLength(songIndex), i);
                    }
                }
            }
        }



        // ===================== Search =====================

        void ExecuteSearch()
        {
            Debug.Log("[YT Tablet] ExecuteSearch");
            bool isArtistSearch = _searchTerm.StartsWith("artist:");
            if(isArtistSearch)
            {
                _resultsSongs = Database.SearchByArtist(_searchTerm.Substring(7).Trim());
                _resultsArtists = new int[]{0,0,0};
            }else
            {
                _resultsSongs = Database.SearchByName(_searchTerm);
                _resultsArtists = Database.SearchArtist(_searchTerm);
            }

            ClearSearchObjects();

            _songsOffset = 0;
            _artistsOffset = 0;

            _local_search_artists_loadSteps = 0;
            _local_search_song_loadSteps = 0;

            ArtistsContainer.parent.gameObject.SetActive(_resultsArtists[0] > 0);
            SongsContainer.parent.gameObject.SetActive(_resultsSongs[0] > 0);

            if(Networking.IsOwner(gameObject))
            {
                _search_songs_loadSteps = 1;
                _search_artists_loadSteps = 1;
                _scrollSearchAbsolute = 0;
                RequestSerialization();
            }

            ShowMoreArtists();
            ShowMoreSongs();

            Debug.Log("[YT Tablet] Done ExecuteSearch");
        }

        void ClearSearchObjects()
        {
            // clear old songs
            foreach (Transform child in SongsContainer)
            {
                Destroy(child.gameObject);
            }
            // clear artists
            foreach (Transform child in ArtistsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        void ShowMoreSongs()
        {
            if(_local_search_song_loadSteps >= _search_songs_loadSteps) return; // Search result length syncing
            Debug.Log("[YT Tablet] ShowMoreSongs");

            bool isArtistSearch = _searchTerm.StartsWith("artist:");
            int listIndex = _songsOffset;
            for(int i = _resultsSongs[1] + _songsOffset; i < _resultsSongs[2] && i < _resultsSongs[1] + _songsOffset + SONGS_PER_LOAD_STEP; i++)
            {
                int index = i;
                if(isArtistSearch) index = Database.GetSongIdFromAristIndices(index);
                ListItemPrefab.Setup(SongsContainer, listIndex, index, Database.GetSongName(index), Database.GetSongArtist(index), Database.GetSongLengthString(index));
                listIndex++;
            }
            _songsOffset += SONGS_PER_LOAD_STEP;
            AdjustContainerHeight(SongsContainer, Mathf.Min(_resultsSongs[0], _songsOffset), 1, 80, 5);
            ButtonSongsShowMore.SetActive(_songsOffset < _resultsSongs[0]);

            // Search result length syncing. Check done after call to handle value changing between frames
            _local_search_song_loadSteps = _songsOffset / SONGS_PER_LOAD_STEP;
            SendCustomEventDelayedFrames(nameof(ShowMoreSongs), 1);
        }

        void ShowMoreArtists()
        {
            if(_local_search_artists_loadSteps >= _search_artists_loadSteps) return; // Search result length syncing
            Debug.Log("[YT Tablet] ShowMoreArtists");

            for (int i = _resultsArtists[1] + _artistsOffset; i < _resultsArtists[2] && i < _resultsArtists[1] + _artistsOffset + ARTISTS_PER_LOAD_STEP; i++)
            {
                string artist = Database.GetArtistName(i);
                CardPrefab.Setup(ArtistsContainer, artist, "Artist", false, i, 
                    _self, nameof(OnArtistSelected), nameof(param_OnArtistSelected), i,
                    null, null, null, null);
            }
            _artistsOffset += ARTISTS_PER_LOAD_STEP;
            AdjustContainerHeight(ArtistsContainer, Mathf.Min(_resultsArtists[0], _artistsOffset), 5, 300, 30);
            ButtonArtistsShowMore.SetActive(_artistsOffset < _resultsArtists[0]);

            // Search result length syncing. Check done after call to handle value changing between frames
            _local_search_artists_loadSteps = _artistsOffset / ARTISTS_PER_LOAD_STEP;
            SendCustomEventDelayedFrames(nameof(ShowMoreArtists), 1);
        }

        void AdjustContainerHeight(Transform contrainer, int elmCount, int countPerRow, int heightPerElm, int spacing)
        {
            int rowCount = (elmCount + countPerRow - 1) / countPerRow;
            RectTransform rect = contrainer.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, rowCount * heightPerElm + Mathf.Max(0, rowCount - 1) * spacing);

            int height = 0;
            foreach(Transform child in contrainer.parent)
            {
                height += (int)child.GetComponent<RectTransform>().sizeDelta.y;
            }
            rect = contrainer.parent.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        // ================== Callbacks ==================
        [HideInInspector] public int param_OnArtistSelected;
        public void OnArtistSelected()
        {
            string name = Database.GetArtistName(param_OnArtistSelected);
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _searchTerm = "artist: " + name;
            SetSearchField();
            //SearchText.text = _searchTerm;
            // TODO
            RequestSerialization();
            ExecuteSearch();
        }

        [HideInInspector] public int param_OnSongShuffle;
        public void OnSongShuffle()
        {
            _isPlaylistOpen = true;
            PlayAndStartNewPlaylist(param_OnSongShuffle);
        }

        [HideInInspector] public int param_OnSongEqueue;
        public void OnSongEqueue()
        {
            if(_playlistLength == 0)
            {
                PlayAndStartNewPlaylist(param_OnSongEqueue);
                return;
            }
            Enqueue(param_OnSongEqueue);
        }

        [HideInInspector] public int param_OnReplaceFirst;
        public void OnReplaceFirst()
        {
            if(_playlistLength == 0)
            {
                PlayAndStartNewPlaylist(param_OnReplaceFirst);
                return;
            }
            ReplacePlaylistHead(param_OnReplaceFirst);
        }

        // ================== Play Functions ==================

        bool _waitingToPlay = false;
        void Play()
        {
            if (_playlist[0] == -1) PlayUrl(_playlistCustomURL[0]);
            else Play(_playlist[0]);
        }

        void Play(int index)
        {
            if (index < 0) return;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            PlayUrl(Database.GetSongURL(index));
        }

        void PlayUrl(VRCUrl url)
        {
            
            VideoUrl = url;
            RequestSerialization();
            if(!_waitingToPlay) // waiting in case of rate limitations by thumbnail loading
            {
                _waitingToPlay = true;
                float waitTime = 0;
                SendCustomEventDelayedSeconds(nameof(SendPlayCommand), waitTime);
                TriggerLoadAnimation(waitTime + 2);
            }
        }

        public void SendPlayCommand()
        {
            _waitingToPlay = false;
            Adapter.SendCustomEvent("Play");
        }

        void TriggerLoadAnimation(float time)
        {
            LoadBarAnimator.SetFloat("speed", 1 / time);
            LoadBarAnimator.SetTrigger("load");
        }

        public void JumpTo(int index)
        {
            if(index < 0) return;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            int playlistIndex = Array.IndexOf(_playlist, index);
            if(playlistIndex >= 0)
            {
                Play(index);
                ShiftPlaylistBy(playlistIndex);
            }else
            {
                PlayAndStartNewPlaylist(index);
            }
        }

        void Enqueue(int index)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            int playlistIndex = Array.IndexOf(_playlist, index);
            int earlistFreeUserIndex = -1;
            for(int i=1;i<_playlist.Length;i++)
            {
                if(_playlistIsUserRequest[i] == false)
                {
                    earlistFreeUserIndex = i;
                    break;
                }
            }
            if (earlistFreeUserIndex >= _playlistLength) earlistFreeUserIndex = _playlistLength;
            Debug.Log(earlistFreeUserIndex);
            if (earlistFreeUserIndex == -1 || earlistFreeUserIndex >= MAX_PLAYLIST_LENGTH) return;
            // if song is already in playlist, move it to earlist position
            if (playlistIndex >= 0)
            {
                if(earlistFreeUserIndex < playlistIndex)
                {
                    int temp = _playlist[earlistFreeUserIndex];
                    _playlist[earlistFreeUserIndex] = _playlist[playlistIndex];
                    _playlistIsUserRequest[earlistFreeUserIndex] = true;
                    _playlistRequestedBy[earlistFreeUserIndex] = Networking.LocalPlayer.displayName;
                    _playlist[playlistIndex] = temp;
                } 
            }else
            {
                _playlist[earlistFreeUserIndex] = index;
                _playlistIsUserRequest[earlistFreeUserIndex] = true;
                _playlistRequestedBy[earlistFreeUserIndex] = Networking.LocalPlayer.displayName;
                if(earlistFreeUserIndex == _playlistLength)
                {
                    _playlistLength++;
                }
            }
            // start playing if nothing is playing
            if(!_isPlaying)
            {
                Play();
            }
            UpdatePlaylist();
            RequestSerialization();
        }

        void ReplacePlaylistHeadWithCustom(VRCUrl url)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _playlist[0] = -1;
            _playlistIsUserRequest[0] = true;
            _playlistRequestedBy[0] = Networking.LocalPlayer.displayName;
            _playlistCustomURL[0] = url;
            PlayUrl(url);
            UpdatePlaylist();
            RequestSerialization();
            SendCustomEventDelayedSeconds(nameof(SendPlayCommand), 0.5f);
        }

        void EnqueueCustom(VRCUrl url)
        {
            Debug.Log("[YT Tablet] EnqueueCustom");
            int earlistFreeUserIndex = -1;
            for (int i = 1; i < _playlist.Length; i++)
            {
                if (_playlistIsUserRequest[i] == false)
                {
                    earlistFreeUserIndex = i;
                    break;
                }
            }
            if (earlistFreeUserIndex >= _playlistLength) earlistFreeUserIndex = _playlistLength;
            Debug.Log(earlistFreeUserIndex);
            if (earlistFreeUserIndex == -1 || earlistFreeUserIndex >= MAX_PLAYLIST_LENGTH) return;
            // if song is already in playlist, move it to earlist position
            _playlist[earlistFreeUserIndex] = -1;
            _playlistIsUserRequest[earlistFreeUserIndex] = true;
            _playlistRequestedBy[earlistFreeUserIndex] = Networking.LocalPlayer.displayName;
            _playlistCustomURL[earlistFreeUserIndex] = url;
            if (earlistFreeUserIndex == _playlistLength)
            {
                _playlistLength++;
            }
            // start playing if nothing is playing
            if (!_isPlaying)
            {
                Play();
            }
            UpdatePlaylist();
            RequestSerialization();
        }

        void ReplacePlaylistHead(int newHead)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _playlist[0] = param_OnReplaceFirst;
            _playlistIsUserRequest[0] = true;
            _playlistRequestedBy[0] = Networking.LocalPlayer.displayName;
            Play(param_OnReplaceFirst);
            UpdatePlaylist();
            RequestSerialization();
        }

        void PlayAndStartNewPlaylist(int index)
        {
            Debug.Log($"[YT Tablet] Starting Playlit with {Database.GetSongName(index)}");
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            Play(index);
            _playlist[0] = index;
            _playlistIsUserRequest[0] = true;
            _playlistRequestedBy[0] = Networking.LocalPlayer.displayName;
            _playlistLength = 1;
            ShiftPlaylistBy(0);
        }

        void PrevSongEnqueue(int song, VRCUrl url)
        {
            _previousSongs[_previousSongsHead] = song;
            _previousUrls[_previousSongsHead] = url;
            _previousSongsHead = (_previousSongsHead + 1) % _previousSongs.Length;
            if(_previousSongsHead == _previousSongsTail)
            {
                _previousSongsTail = (_previousSongsTail + 1) % _previousSongs.Length;
            }
        }

        int PrevSongLength()
        {
            if(_previousSongsHead == _previousSongsTail) return 0;
            if(_previousSongsHead > _previousSongsTail) return _previousSongsHead - _previousSongsTail;
            return _previousSongs.Length - _previousSongsTail + _previousSongsHead;
        }

        VRCUrl _previous_url_result;
        int PrevSongDequeue()
        {
            if(_previousSongsHead == _previousSongsTail) return -1;
            _previousSongsHead = (_previousSongsHead - 1 + _previousSongs.Length) % _previousSongs.Length;
            int song = _previousSongs[_previousSongsHead];
            _previous_url_result = _previousUrls[_previousSongsHead];
            return song;
        }

        int[] GetNextAutogenSong()
        {
            // prioritize songs requested by users
            for(int i=0;i<_playlistLength;i++)
            {
                if(_playlist[i] >= 0 && _playlistIsUserRequest[i])
                {
                    int songId = Database.GetRandomRealtedNotInList(_playlist[i], _playlist, _playlistLength);
                    if(songId >= 0) return new int[]{ songId, i };
                }
            }
            // else get get random related song from any song in playlist not in list
            int start = UnityEngine.Random.Range(0, _playlistLength);
            for(int i=0;i<_playlist.Length;i++)
            {
                int j = (start + i) % _playlistLength;
                if(_playlist[i] < 0) continue;
                int songId = Database.GetRandomRealtedNotInList(_playlist[j], _playlist, _playlistLength);
                if(songId >= 0) return new int[]{ songId, j };
            }
            // else get first related song from any song in playlist
            for(int i=0;i<_playlist.Length;i++)
            {
                if(_playlist[i] < 0) continue;
                int songId = Database.GetRandomRelated(i);
                if(songId >= 0) return new int[]{ songId, i };
            }
            return new int[]{ -1, -1 };
        }

        void ShiftPlaylistBy(int count)
        {
            for(int i = 0; i < count; i++)
            {
                PrevSongEnqueue(_playlist[i], _playlistCustomURL[i]);
            }
            _playlistLength = _playlistLength - count;
            for(int i = 0; i < _playlistLength; i++)
            {
                _playlist[i] = _playlist[i + count];
                _playlistIsUserRequest[i] = _playlistIsUserRequest[i + count];
                _playlistRequestedBy[i] = _playlistRequestedBy[i + count];
                _playlistCustomURL[i] = _playlistCustomURL[i + count];
            }
            for(int i = _playlistLength; i < PLAYLIST_AUTO_GEN_LENGTH; i++)
            {
                int[] autoGen = GetNextAutogenSong();
                _playlist[i] = autoGen[0];
                if(autoGen[0] < 0) break; // this should never happen
                _playlistIsUserRequest[i] = false;
                _playlistRequestedBy[i] = "Auto: " + Database.GetSongArtist(_playlist[autoGen[1]]); // list artist as requester
                _playlistLength = i + 1;
            }
            UpdatePlaylist();
            RequestSerialization();
        }

        public void VideoPlayerHasBeenPaused()
        {
            _isPlaying = false;
            PauseImage.gameObject.SetActive(_isPlaying);
            PlayImage.gameObject.SetActive(!_isPlaying);
        }

        public void VideoPlayerHasBeenResumed()
        {
            _isPlaying = true;
            PauseImage.gameObject.SetActive(_isPlaying);
            PlayImage.gameObject.SetActive(!_isPlaying);
        }

        public void OnPauseReumeButton()
        {
            if(_isPlaying) Adapter.SendCustomEvent("Pause");
            else Adapter.SendCustomEvent("Resume");
        }

        public void Next()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            if(_playlistLength == 0) return;
            ShiftPlaylistBy(1);
            SendPlayRequest();
        }

        public void Previous()
        {
            if(PrevSongLength() > 0)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                int index = PrevSongDequeue();
                _playlistLength = Mathf.Min(_playlistLength + 1, _playlist.Length);
                for(int i = _playlistLength - 1; i > 0; i--)
                {
                    _playlist[i] = _playlist[i - 1];
                    _playlistIsUserRequest[i] = _playlistIsUserRequest[i - 1];
                    _playlistRequestedBy[i] = _playlistRequestedBy[i - 1];
                    _playlistCustomURL[i] = _playlistCustomURL[i - 1];
                }
                _playlist[0] = index;
                _playlistIsUserRequest[0] = true;
                _playlistRequestedBy[0] = Networking.LocalPlayer.displayName;
                _playlistCustomURL[0] = _previous_url_result;
                SendPlayRequest();
                UpdatePlaylist();
                RequestSerialization();
            }
        }

        // Rate Limiting Skips
        // Adding a delay to manual skip requests to prevent spamming of video player which results in the wrong video playing
        void SendPlayRequest()
        {
            _skipCallCount++;
            SendCustomEventDelayedSeconds(nameof(HandleSkipPlayRequest), 0.5f);
        }

        public void HandleSkipPlayRequest()
        {
            _skipCallCount--;
            if(_skipCallCount == 0)
            {
                Play();
            }
        }
    }
}
