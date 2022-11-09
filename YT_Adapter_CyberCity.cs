
using UdonSharp;
using UdonSharp.Video;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Thry.YTDB
{
    public class YT_Adapter_CyberCity : UdonSharpBehaviour
    {
        public YT_Tablet Tablet;
        USharpVideoPlayer _videoPlayer;
        VRCUrl _videoUrl;

        void ResolveVideoPlayer()
        {
            if(_videoPlayer == null || !_videoPlayer.gameObject.activeInHierarchy)
            {
                Appartment appartment = GetComponentInParent<Appartment>();
                if(appartment != null)
                {
                    ApparmentNetworking apparmentNetworking = appartment.net;
                    if(apparmentNetworking != null)
                    {
                        Furniture[] furniture = apparmentNetworking.localFurnitureReference;
                        if(furniture != null)
                        {
                            foreach(Furniture furnitureItem in furniture)
                            {
                                if(furnitureItem != null)
                                {
                                    _videoPlayer = furnitureItem.GetComponentInChildren<USharpVideoPlayer>();
                                    if(_videoPlayer != null && _videoPlayer.gameObject.activeInHierarchy)
                                    {
                                        _videoPlayer.RegisterCallbackReceiver(this);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Play()
        {
            ResolveVideoPlayer();
            VRCUrl url = Tablet.VideoUrl;
            if(_videoPlayer != null && !(string.IsNullOrEmpty(url.Get())))
            {
                _videoUrl = url;
                _videoPlayer.PlayVideo(_videoUrl);
            }
        }

        public void OnUSharpVideoEnd()
        {
            Debug.Log("[Thry][YTDB] OnUSharpVideoEnd");
            if(_videoPlayer != null && _videoUrl != null)
            {
                // Debug.Log("URL on player is " + _videoPlayer.GetCurrentURL().Get());
                // Debug.Log("URL on tablet is " + _videoUrl);
                // Debug.Log("Equals? " + _videoUrl.Equals(_videoPlayer.GetCurrentURL()));
                if(_videoUrl.Equals(_videoPlayer.GetCurrentURL()))
                {
                    Tablet.Next();
                }
            }
        }
    }
}