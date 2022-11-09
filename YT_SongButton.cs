﻿
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Thry.YTDB
{
    public class YT_SongButton : UdonSharpBehaviour
    {
        int _songIndex;
        YT_Tablet _tablet;

        public UnityEngine.UI.Text SongNameText;
        public UnityEngine.UI.Text ArtistText;
        public UnityEngine.UI.Text LengthText;
        public UnityEngine.UI.Text IndexText;

        public void Setup(YT_Tablet tablet, int songIndex, string songName, string artist, int length, int listIndex)
        {
            _tablet = tablet;
            _songIndex = songIndex;
            SongNameText.text = songName;
            ArtistText.text = artist;
            LengthText.text = length / 60 + ":" + (length % 60).ToString("00");
            IndexText.text = (listIndex + 1).ToString();
        }

        public void Play()
        {
            _tablet.JumpTo(_songIndex);
        }
    }

}
