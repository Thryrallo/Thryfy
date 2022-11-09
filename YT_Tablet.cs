
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Thry.YTDB
{
    public class YT_Tablet : UdonSharpBehaviour
    {
        const int PLAYLIST_AUTO_GEN_LENGTH = 10;
        const int MAX_PLAYLIST_LENGTH = 100;
        const int LOAD_STEPS = 12;

        public string InitialSearch = "Taylor";
        public Animator LoadBarAnimator;
        public YT_DB_Manager DatabaseManager;
        public UdonBehaviour Adapter;
        public ThumbnailLoader ThubmnailLoader;
        public YT_Card CardPrefab;
        public YT_SongButton SongButtonPrefab;
        public Transform SongsContainer;
        public Transform ArtistsContainer;
        public GameObject ButtonSongsShowMore;
        public GameObject ButtonArtistsShowMore;
        public UnityEngine.UI.InputField SearchText;

        public Image PlayImage;
        public Image PauseImage;

        public Transform PlaylistContainer;

        [HideInInspector]
        [UdonSynced] public VRCUrl VideoUrl;

        [UdonSynced] string _searchTerm = "";
        [UdonSynced] bool _isSongNameSearch = true;
        [UdonSynced] int[] _playlist = new int[MAX_PLAYLIST_LENGTH];
        [UdonSynced] bool[] _playlistIsUserRequest = new bool[MAX_PLAYLIST_LENGTH];
        [UdonSynced] int _playlistLength = 0;
        [UdonSynced] int[] _previousSongs = new int[100];
        [UdonSynced] int _previousSongsHead = 0;
        [UdonSynced] int _previousSongsTail = 0;
        int[] _localPlaylistIndex = new int[MAX_PLAYLIST_LENGTH];
        [UdonSynced] int _currentSongIndex = -1;

        int[] _resultsSongs;
        int[] _resultsArtists;
        int songsOffset = 0;
        int artistsOffset = 0;
        bool _isPlaying = false;
        YT_DB _db;
        UdonBehaviour _self;
        YT_SongButton[] _playlistButtons = new YT_SongButton[MAX_PLAYLIST_LENGTH];
        int _skipCallCount = 0;

        private void Start() 
        {
            _db = DatabaseManager.Database;
            _self = this.GetComponent<UdonBehaviour>();
            if(Networking.IsOwner(gameObject))
            {
                SearchText.text = InitialSearch;
                OnInputChanged();
            }
        }

        public void OnInputChanged()
        {
            if(_searchTerm == SearchText.text)
            {
                return;
            }

            Networking.SetOwner(Networking.LocalPlayer, gameObject);

            _searchTerm = SearchText.text;

            RequestSerialization();
            ExecuteSearch();
        }

        public override void OnDeserialization()
        {
            UpdatePlaylist();
            if(_searchTerm == SearchText.text)
            {
                return;
            }
            SearchText.text = _searchTerm;
            ExecuteSearch();
        }

        void UpdatePlaylist()
        {
            for(int i = 0;i < _playlistLength; i++)
            {
                if(_playlistButtons[i] == null)
                {
                    _playlistButtons[i] = Instantiate(SongButtonPrefab.gameObject, PlaylistContainer).GetComponent<YT_SongButton>();
                    _playlistButtons[i].gameObject.SetActive(true);
                    _localPlaylistIndex[i] = -1;
                }
                if(_localPlaylistIndex[i] != _playlist[i])
                {
                    _localPlaylistIndex[i] = _playlist[i];
                    int songIndex = _playlist[i];
                    _playlistButtons[i].Setup(this, songIndex, _db.GetSongName(songIndex), _db.GetSongArtist(songIndex), _db.GetSongLength(songIndex), i);
                }
            }
        }

        // ===================== Search =====================

        void ExecuteSearch()
        {
            bool isArtistSearch = _searchTerm.StartsWith("artist:");
            if(isArtistSearch)
            {
                _resultsSongs = _db.SearchByArtist(_searchTerm.Substring(7).Trim());
                _resultsArtists = new int[]{0,0,0};
            }else
            {
                _resultsSongs = _db.SearchByName(_searchTerm);
                _resultsArtists = _db.SearchArtist(_searchTerm);
            }
            

            // clear old songs
            foreach(Transform child in SongsContainer)
            {
                Destroy(child.gameObject);
            }
            // clear artists
            foreach(Transform child in ArtistsContainer)
            {
                Destroy(child.gameObject);
            }

            songsOffset = 0;
            artistsOffset = 0;

            ArtistsContainer.parent.gameObject.SetActive(_resultsArtists[0] > 0);
            SongsContainer.parent.gameObject.SetActive(_resultsSongs[0] > 0);

            ShowMoreArtists();
            ShowMoreSongs();
        }

        void ShowMoreSongs()
        {
            bool isArtistSearch = _searchTerm.StartsWith("artist:");
            for(int i = _resultsSongs[1] + songsOffset; i < _resultsSongs[2] && i < _resultsSongs[1] + songsOffset + LOAD_STEPS; i++)
            {
                int index = i;
                if(isArtistSearch) index = _db.GetSongIdFromAristIndices(index);
                CardPrefab.Setup(SongsContainer, _db.GetSongName(index), _db.GetSongArtist(index), true, _db.GetSongURL(index), 
                    _self, nameof(OnSongEqueue), nameof(param_OnSongEqueue), index,
                    _self, nameof(OnSongPlay), nameof(param_OnSongPlay), index);
            }
            songsOffset += LOAD_STEPS;
            AdjustContainerHeight(SongsContainer, Mathf.Min(_resultsSongs[0], songsOffset));
            ButtonSongsShowMore.SetActive(songsOffset < _resultsSongs[0]);
        }

        void ShowMoreArtists()
        {
            for(int i = _resultsArtists[1] + artistsOffset; i < _resultsArtists[2] && i < _resultsArtists[1] + artistsOffset + LOAD_STEPS; i++)
            {
                string artist = _db.GetArtist(i);
                int[] songs = _db.SearchByArtist(artist);
                VRCUrl thumbnailUrl = _db.GetSongURL(_db.GetSongIdFromAristIndices(UnityEngine.Random.Range(songs[1], songs[2])));
                CardPrefab.Setup(ArtistsContainer, artist, "Artist", false, thumbnailUrl, 
                    _self, nameof(OnArtistSelected), nameof(param_OnArtistSelected), i,
                    null, null, null, null);
            }
            artistsOffset += LOAD_STEPS;
            AdjustContainerHeight(ArtistsContainer, Mathf.Min(_resultsArtists[0], artistsOffset));
            ButtonArtistsShowMore.SetActive(artistsOffset < _resultsArtists[0]);
        }

        public void LoadMoreSongs()
        {
            ShowMoreSongs();
        }

        public void LoadMoreArtists()
        {
            ShowMoreArtists();
        }

        void AdjustContainerHeight(Transform contrainer, int elmCount)
        {
            int height = (int)((elmCount + 3) / 4);
            RectTransform rect = contrainer.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height * 300);

            rect = contrainer.parent.GetComponent<RectTransform>();
            foreach(Transform child in contrainer.parent)
            {
                height += (int)child.GetComponent<RectTransform>().sizeDelta.y;
            }
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        // ================== Callbacks ==================
        [HideInInspector] public int param_OnArtistSelected;
        public void OnArtistSelected()
        {
            string name = _db.GetArtist(param_OnArtistSelected);
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _searchTerm = "artist: " + name;
            SearchText.text = _searchTerm;
            RequestSerialization();
            ExecuteSearch();
        }

        [HideInInspector] public int param_OnSongPlay;
        public void OnSongPlay()
        {
            PlayAndStartNewPlaylist(param_OnSongPlay);
        }

        [HideInInspector] public int param_OnSongEqueue;
        public void OnSongEqueue()
        {
            Enqueue(param_OnSongEqueue);
        }

        // ================== Play Functions ==================

        bool _waitingToPlay = false;
        void Play(int index)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _currentSongIndex = index;
            VideoUrl = _db.GetSongURL(index);
            RequestSerialization();
            if(!_waitingToPlay) // waiting in case of rate limitations by thumbnail loading
            {
                _waitingToPlay = true;
                float waitTime = ThubmnailLoader.GetRetryTime();
                SendCustomEventDelayedSeconds(nameof(SendPlayCommand), waitTime);
                ThubmnailLoader.TimeoutReqeusts(waitTime + 5.5f);
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
            if(_playlistLength == 0)
            {
                PlayAndStartNewPlaylist(index);
                return;
            }
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
            if(earlistFreeUserIndex == -1) return;
            // if song is already in playlist, move it to second position
            if(playlistIndex >= 0 && earlistFreeUserIndex < playlistIndex)
            {
                int temp = _playlist[earlistFreeUserIndex];
                _playlist[earlistFreeUserIndex] = _playlist[playlistIndex];
                _playlistIsUserRequest[earlistFreeUserIndex] = true;
                _playlist[playlistIndex] = temp;
            }else
            {
                _playlist[earlistFreeUserIndex] = index;
                _playlistIsUserRequest[earlistFreeUserIndex] = true;
            }
            // start playing if nothing is playing
            if(!_isPlaying)
            {
                Next();
            }
            UpdatePlaylist();
            RequestSerialization();
        }

        void PlayAndStartNewPlaylist(int index)
        {
            Play(index);
            _playlist[0] = index;
            _playlistLength = 1;
            ShiftPlaylistBy(0);
        }

        void PrevSongEnqueue(int song)
        {
            _previousSongs[_previousSongsHead] = song;
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

        int PrevSongDequeue()
        {
            if(_previousSongsHead == _previousSongsTail) return -1;
            _previousSongsHead = (_previousSongsHead - 1 + _previousSongs.Length) % _previousSongs.Length;
            int song = _previousSongs[_previousSongsHead];
            return song;
        }

        void ShiftPlaylistBy(int count)
        {
            for(int i = 0; i < count; i++)
            {
                PrevSongEnqueue(_playlist[i]);
            }
            _playlistLength = _playlistLength - count;
            for(int i = 0; i < _playlistLength; i++)
            {
                _playlist[i] = _playlist[i + count];
            }
            for(int i = _playlistLength; i < PLAYLIST_AUTO_GEN_LENGTH; i++)
            {
                _playlist[i] = _db.GetRandomRelated(_playlist[i - 1]);
            }
            _playlistLength = Mathf.Max(_playlistLength, PLAYLIST_AUTO_GEN_LENGTH);
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
            SendSkipPlayRequest(_playlist[1]);
            ShiftPlaylistBy(1);
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
                }
                _playlist[0] = index;
                _playlistIsUserRequest[0] = true;
                SendSkipPlayRequest(index);
                UpdatePlaylist();
                RequestSerialization();
            }
        }

        // Rate Limiting Skips
        // Adding a delay to manual skip requests to prevent spamming of video player which results in the wrong video playing
        void SendSkipPlayRequest(int index)
        {
            _skipCallCount++;
            _currentSongIndex = index;
            SendCustomEventDelayedSeconds(nameof(HandleSkipPlayRequest), 0.5f);
        }

        public void HandleSkipPlayRequest()
        {
            _skipCallCount--;
            if(_skipCallCount == 0)
            {
                Play(_currentSongIndex);
            }
        }
    }
}
