
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Components.Video;
using System;

namespace Thry.YTDB
{
    public class ThumbnailLoader : UdonSharpBehaviour
    {
        public VRCUnityVideoPlayer VideoPlayer;
        public RenderTexture TargetTexture;
        public RenderTexture[] ThumbnailTextures;
        public YT_DB_Manager Manager;

        RenderTexture[] _artistIndicesToTextures;
        int[] _textureIndicesToArtistIndices;
        int[] _renderTextureUses;
        bool[] _needsLoading;
        int[] _requestId; // to execute the request in the correct order
        float[] _lastUsed;
        int _lastRequestId = 0;

        VRCUrl _currentUrl;
        int _currentlyLoadingIndex = -1;
        float _lastSoppedTime;
        bool _isPlaying = false;
        bool _doesAnyRequireLoading = false;
        
        bool _isWaitingForVideoReady = false;

        float _minTimeForNextLoad = 0;
        float _lastLoadRequestTime = 0;

        public void TimeoutReqeusts(float timeout)
        {
            _minTimeForNextLoad = Time.time + timeout;
        }

        public float GetRetryTime()
        {
            return Mathf.Max(0, 5.5f - (Time.time - _lastLoadRequestTime));
        }

        public RenderTexture GetTexture(int artistIndex)
        {
            RenderTexture rt = _artistIndicesToTextures[artistIndex];
            if(rt == null)
            {
                // find index with 0 last use time => never used
                int index = Array.IndexOf(_lastUsed, 0);
                // if none esist get the oldest one that has zero uses
                float earlist = float.MaxValue;
                if(index == -1)
                {
                    for(int i = 0; i < _lastUsed.Length; i++)
                    {
                        if(_renderTextureUses[i] == 0)
                        {
                            if(_lastUsed[i] < earlist)
                            {
                                index = i;
                                earlist = _lastUsed[i];
                            }
                        }
                    }
                }
                // if still none found get the one with the lowest use count
                if(index == -1)
                {
                    // find index with lowest use count
                    int lowestUseCount = int.MaxValue;
                    for(int i = 0; i < _renderTextureUses.Length; i++)
                    {
                        if(_renderTextureUses[i] < lowestUseCount)
                        {
                            lowestUseCount = _renderTextureUses[i];
                            index = i;
                        }
                    }
                }

                rt = ThumbnailTextures[index];
                _renderTextureUses[index] = 1;
                _needsLoading[index] = true;
                _artistIndicesToTextures[artistIndex] = rt;
                _textureIndicesToArtistIndices[index] = artistIndex;
                _doesAnyRequireLoading = true;
                _requestId[index] = _lastRequestId++;
                _lastUsed[index] = Time.time;
            }else
            {
                int index = Array.IndexOf(ThumbnailTextures, rt);
                _renderTextureUses[index]++;
            }
            return rt;
        }

        public void UnregisterUse(int artistId)
        {
            RenderTexture rt = _artistIndicesToTextures[artistId];
            if(rt != null)
            {
                int index = Array.IndexOf(ThumbnailTextures, rt);
                if(index != -1)
                {
                    _renderTextureUses[index]--;
                    if(_renderTextureUses[index] == 0)
                    {
                        _needsLoading[index] = false;
                        _lastUsed[index] = Time.time;

                        _doesAnyRequireLoading = false;
                        for(int i = 0; i < _needsLoading.Length; i++)
                        {
                            _doesAnyRequireLoading |= _needsLoading[i];
                        }
                    }
                }
            }
        }

        // ==================== Internal ====================

        private void Start() 
        {
            Manager.Database.LoadAssets();
            int artists = Manager.Database.GetArtistCount();
            _artistIndicesToTextures = new RenderTexture[artists];
            _renderTextureUses = new int[ThumbnailTextures.Length];
            _needsLoading = new bool[ThumbnailTextures.Length];
            _requestId = new int[ThumbnailTextures.Length];
            _lastUsed = new float[ThumbnailTextures.Length];
            _textureIndicesToArtistIndices = new int[ThumbnailTextures.Length];
            for(int i = 0; i < ThumbnailTextures.Length; i++)
            {
                _requestId[i] = Int16.MaxValue;
            }
        }

        public override void OnVideoError(VideoError videoError)
        {
            if(videoError == VideoError.RateLimited && _isPlaying)
            {
                SendCustomEventDelayedSeconds(nameof(RateLimitedLoadRequest), 1);
            }else
            {
                VideoPlayer.Stop();
                _lastSoppedTime = Time.time;
                _isPlaying = false;
                _isWaitingForVideoReady = false;
            }
        }

        public void RateLimitedLoadRequest()
        {
            if(Time.time < _minTimeForNextLoad)
                SendCustomEventDelayedSeconds(nameof(RateLimitedLoadRequest), _minTimeForNextLoad - Time.time + 0.05f);
            else if(Time.time - _lastLoadRequestTime < 5)
                SendCustomEventDelayedSeconds(nameof(RateLimitedLoadRequest), 5 - (Time.time - _lastLoadRequestTime) + 0.05f);
            else
            {
                _lastLoadRequestTime = Time.time;
                VideoPlayer.PlayURL(_currentUrl);
            } 
        }

        public override void OnVideoReady()
        {
            _isWaitingForVideoReady = false;
        }

        void FindNextLoadRequest()
        {
            _currentlyLoadingIndex = -1;
            int lowestRequestId = int.MaxValue;
            for(int i = 0; i < _needsLoading.Length; i++)
            {
                if(_needsLoading[i] && _requestId[i] < lowestRequestId)
                {
                    _currentlyLoadingIndex = i;
                    lowestRequestId = _requestId[i];
                }
            }
            if(_currentlyLoadingIndex == -1)
                _currentUrl = null;
            else
            {
                string artistName = Manager.Database.GetArtistName(_textureIndicesToArtistIndices[_currentlyLoadingIndex]);
                int[] songs = Manager.Database.SearchByArtist(artistName);
                _currentUrl = Manager.Database.GetSongURL(Manager.Database.GetSongIdFromAristIndices(songs[1]));
            }
        }

        void Update()
        {
            if(_isWaitingForVideoReady)
                return;
            if(_currentlyLoadingIndex > -1)
            {
                if(VideoPlayer.GetTime() > 0.1f)
                {
                    RenderTexture tex = ThumbnailTextures[_currentlyLoadingIndex];
                    VRCGraphics.Blit(TargetTexture, tex, new Vector2(1,1), new Vector2(0,0));
                    _needsLoading[_currentlyLoadingIndex] = false;
                    _currentlyLoadingIndex = -1;
                    _currentUrl = null;
                    VideoPlayer.Stop();
                    _lastSoppedTime = Time.time;
                    _isPlaying = false;
                }
                return;
            }
            if(_currentlyLoadingIndex == -1 && _isPlaying)
            {
                VideoPlayer.Stop();
                _lastSoppedTime = Time.time;
                _isPlaying = false;
                return;
            }
            if(!_doesAnyRequireLoading)
                return;
            if(Time.time - _lastSoppedTime < 0.3f)
                return;
            FindNextLoadRequest();
            if(_currentUrl == null)
                return;
            _isPlaying = true;
            _isWaitingForVideoReady = true; // this ordering is important, the vars need to be set before Play, because rate limit errors will be thrown immediately
            RateLimitedLoadRequest();
        }
    }

}