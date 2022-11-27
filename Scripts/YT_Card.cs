
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Thry.YTDB
{
    public class YT_Card : UdonSharpBehaviour
    {
        public ThumbnailLoader ThumbnailLoaderManager;
        public UnityEngine.UI.Text Major;
        public UnityEngine.UI.Text Minor;
        public UnityEngine.UI.RawImage Thumbnail;
        public UnityEngine.UI.Image ThumbnailBackground;
        public UnityEngine.UI.Button SecondaryButton;
        public Sprite DefaultThumbnailRound;
        public Sprite DefaultThumbnailSquare;
        public Sprite ThumbnailBackgroundRound;
        public Sprite ThumbnailBackgroundSquare;

        int _artistId;

        UdonBehaviour _callbackPrimaryBehaviour;
        string _callbackPrimaryMethod;
        string _callbackPrimaryField;
        object _callbackPrimaryValue;

        UdonBehaviour _callbackSecondaryBehaviour;
        string _callbackSecondaryMethod;
        string _callbackSecondaryField;
        object _callbackSecondaryValue;

        public void Setup(Transform parent, string major, string minor, bool isRect, int artistId, 
            UdonBehaviour callbackPrimaryBehaviour, string callbackPrimaryMethod, string callbackPrimaryField, object callbackPrimaryValue,
            UdonBehaviour callbackSecondaryBehaviour, string callbackSecondaryMethod, string callbackSecondaryField, object callbackSecondaryValue)
        {
            GameObject instance = Instantiate(gameObject, parent);
            YT_Card card = instance.GetComponent<YT_Card>();
            card._artistId = artistId;
            card.Major.text = major;
            card.Minor.text = minor;
            card.ThumbnailBackground.sprite = isRect ? ThumbnailBackgroundSquare : ThumbnailBackgroundRound;
            card._callbackPrimaryBehaviour = callbackPrimaryBehaviour;
            card._callbackPrimaryMethod = callbackPrimaryMethod;
            card._callbackPrimaryField = callbackPrimaryField;
            card._callbackPrimaryValue = callbackPrimaryValue;
            card._callbackSecondaryBehaviour = callbackSecondaryBehaviour;
            card._callbackSecondaryMethod = callbackSecondaryMethod;
            card._callbackSecondaryField = callbackSecondaryField;
            card._callbackSecondaryValue = callbackSecondaryValue;
            card.SecondaryButton.gameObject.SetActive(callbackSecondaryBehaviour != null);
            if(artistId > -1) card.Thumbnail.texture = ThumbnailLoaderManager.GetTexture(artistId);
            instance.SetActive(true);
        }

        private void OnDestroy() 
        {
            if(_artistId > -1) ThumbnailLoaderManager.UnregisterUse(_artistId);
        }

        public void OnPrimary()
        {
            if(_callbackPrimaryBehaviour != null)
            {
                _callbackPrimaryBehaviour.SetProgramVariable(_callbackPrimaryField, _callbackPrimaryValue);
                _callbackPrimaryBehaviour.SendCustomEvent(_callbackPrimaryMethod);
            }
        }

        public void OnSecondary()
        {
            if(_callbackSecondaryBehaviour != null)
            {
                _callbackSecondaryBehaviour.SetProgramVariable(_callbackSecondaryField, _callbackSecondaryValue);
                _callbackSecondaryBehaviour.SendCustomEvent(_callbackSecondaryMethod);
            }
        }
    }

}