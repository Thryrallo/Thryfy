
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Components.Video;

namespace Thry.YTDB
{
    public class ThumbnailLoader : UdonSharpBehaviour
    {
        public VRCUnityVideoPlayer VideoPlayer;
        public RenderTexture TargetTexture;
        public RenderTexture[] ThumbnailTextures;

        int _nextThumbnailIndex = 0;

        YT_Card[] _queue = new YT_Card[100];
        int _queueHead;
        int _queueTail;
        int _queueSize;

        YT_Card _currentCard;
        VRCUrl _currentUrl;
        float _lastSoppedTime;
        bool _isPlaying = false;
        
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
        
        public void Request(YT_Card card)
        {
            _queue[_queueHead] = card;
            _queueHead = (_queueHead + 1) % _queue.Length;
            _queueSize = Mathf.Min(_queueSize + 1, _queue.Length);
            if(_queueHead == _queueTail)
                _queueTail = (_queueTail + 1) % _queue.Length;
        }

        YT_Card Dequeue()
        {
            YT_Card card = null;
            // dequeue till card is not null or queue is empty
            while(card == null)
            {
                if(_queueHead == _queueTail)
                    return null;
                card = _queue[_queueTail];
                _queueTail = (_queueTail + 1) % _queue.Length;
                _queueSize = Mathf.Max(_queueSize - 1, 0);
            }
            return card;
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

        void Update()
        {
            if(_isWaitingForVideoReady)
                return;
            if(_currentCard != null)
            {
                if(VideoPlayer.GetTime() > 0.1f)
                {
                    RenderTexture tex = ThumbnailTextures[_nextThumbnailIndex];
                    _nextThumbnailIndex = (_nextThumbnailIndex + 1) % ThumbnailTextures.Length;
                    VRCGraphics.Blit(TargetTexture, tex, new Vector2(1,1), new Vector2(0,0));
                    _currentCard.SetThumbnailTexture(tex);
                    _currentCard = null;
                    VideoPlayer.Stop();
                    _lastSoppedTime = Time.time;
                    _isPlaying = false;
                }
                return;
            }
            if(_currentCard == null && _isPlaying)
            {
                VideoPlayer.Stop();
                _lastSoppedTime = Time.time;
                _isPlaying = false;
                return;
            }
            if(_queueSize == 0)
                return;
            if(Time.time - _lastSoppedTime < 0.3f)
                return;
            _currentCard = Dequeue();
            if(_currentCard == null)
                return;
            _currentUrl = _currentCard.GetThumbnailUrl();
            _isPlaying = true;
            _isWaitingForVideoReady = true; // this ordering is important, the vars need to be set before Play, because rate limit errors will be thrown immediately
            RateLimitedLoadRequest();
        }
    }

}