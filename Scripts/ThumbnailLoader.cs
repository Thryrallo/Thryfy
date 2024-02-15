
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Components.Video;
using System;
using VRC.SDK3.Image;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Common;

namespace Thry.YTDB
{
    public class ThumbnailLoader : UdonSharpBehaviour
    {
        public YT_DB Database;
        
        private VRCImageDownloader _imageDownloader;

        private bool _isInit = false;

        void Init()
        {
            if (_isInit) return;
            _imageDownloader = new VRCImageDownloader();
            _isInit = true;
        }

        public void RequestTexture(int artistIndex, UdonBehaviour callback)
        {
            Init();
            Debug.Log("RequestTexture " + artistIndex);
            VRCUrl url = Database.GetArtistURL(artistIndex);
            Debug.Log("RequestTexture " + url);
            IVRCImageDownload dl = _imageDownloader.DownloadImage(url, null, callback);
        }
    }

}