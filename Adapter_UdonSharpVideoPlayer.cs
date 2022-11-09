
using UdonSharp;
using UdonSharp.Video;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Thry.YTDB
{
    public class Adapter_UdonSharpVideoPlayer : UdonSharpBehaviour
    {
        public YT_Tablet Tablet;
        public USharpVideoPlayer VideoPlayer;
        VRCUrl _videoUrl;

        private void Start() 
        {
            VideoPlayer.RegisterCallbackReceiver(this);
        }

        public void Play()
        {
            VRCUrl url = Tablet.VideoUrl;
            if(VideoPlayer != null && !(string.IsNullOrEmpty(url.Get())))
            {
                _videoUrl = url;
                VideoPlayer.PlayVideo(_videoUrl);
            }
        }

        public void Pause()
        {
            if(VideoPlayer != null)
            {
                VideoPlayer.SetPaused(true);
            }
        }

        public void Resume()
        {
            if(VideoPlayer != null)
            {
                VideoPlayer.SetPaused(false);
            }
        }

        public void OnUSharpVideoPause()
        {
            Tablet.VideoPlayerHasBeenPaused();
        }

        public void OnUSharpVideoUnpause()
        {
            Tablet.VideoPlayerHasBeenResumed();
        }

        public void OnUSharpVideoPlay()
        {
            Tablet.VideoPlayerHasBeenResumed();
        }

        bool _isRateLimited = false;
        private void Update() 
        {
            string status = (string)VideoPlayer.GetProgramVariable("_lastStatusText");
            bool isRateLimited = status == "Rate limited, retrying...";
            if(_isRateLimited != isRateLimited)
            {
                _isRateLimited = isRateLimited;
                if(_isRateLimited)
                    Tablet.ThubmnailLoader.TimeoutReqeusts(5.7f); // usharp retry time is 5.5s
            }
        }

        public void OnUSharpVideoEnd()
        {
            Debug.Log("[Thry][YTDB] OnUSharpVideoEnd");
            if(VideoPlayer != null && _videoUrl != null)
            {
                // Debug.Log("URL on player is " + _videoPlayer.GetCurrentURL().Get());
                // Debug.Log("URL on tablet is " + _videoUrl);
                // Debug.Log("Equals? " + _videoUrl.Equals(_videoPlayer.GetCurrentURL()));
                if(_videoUrl.Equals(VideoPlayer.GetCurrentURL()))
                {
                    Tablet.Next();
                }else
                {
                    Tablet.VideoPlayerHasBeenPaused();
                }
            }
        }
    }
}
