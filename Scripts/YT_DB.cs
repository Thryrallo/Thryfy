
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UdonSharpEditor;
#endif

namespace Thry.YTDB
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class YT_DB : UdonSharpBehaviour
    {
        // data is sorted by name in alphabetical order
        string[] _names;
        string[] _artists;
        [SerializeField, HideInInspector] private VRCUrl[] _urls;
        [SerializeField, HideInInspector] private int[] _artistIndices;
        [SerializeField, HideInInspector] private int[] _related; // 5 per song
        [SerializeField, HideInInspector] private ushort[] _length;

        [SerializeField, HideInInspector] private int[] _artistToSongIndices_artistIds;
        [SerializeField, HideInInspector] private int[] _artistToSongIndices_songIndices;
        [SerializeField, HideInInspector] private TextAsset _songAsset;
        [SerializeField, HideInInspector] private TextAsset _artistAsset;

        bool _isLoaded;

        private void Start() 
        {
            LoadAssets();
        }

        public void LoadAssets()
        {
            if(_isLoaded) return;
            _names = _songAsset.text.Split('\n');
            _artists = _artistAsset.text.Split('\n');
            _isLoaded = true;
        }

        public int[] SearchByName(string name)
        {
            name = name.ToLower();
            return BinarySearch(_names, name);
        }

        public int[] SearchByArtist(string artist)
        {
            artist = artist.ToLower();
            return BinarySearchByArtist(artist);
        }

        public int[] SearchArtist(string name)
        {
            name = name.ToLower();
            return BinarySearch(_artists, name);
        }

        int[] BinarySearch(string[] ar, string term)
        {
            // binary search, match beggining of string
            int min = 0;
            int max = ar.Length - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                string midVal = ar[mid].ToLower();
                int cmp = midVal.CompareTo(term);
                bool match = midVal.StartsWith(term);
                if (cmp == 0 || match)
                {
                    int minIndex = BinarySearchMin(ar, term, min, max, mid);
                    int maxIndex = BinarySearchMax(ar, term, min, max, mid);
                    if(minIndex == -1 || maxIndex == -1)
                    {
                        return new int[]{0,0,0};
                    }
                    return new int[] { maxIndex - minIndex + 1, minIndex, maxIndex + 1 };
                }
                else if (cmp < 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }
            return new int[]{0,0,0};
        }

        int BinarySearchMin(string[] ar, string term, int min, int max, int mid)
        {
            max = mid;
            while(min <= max)
            {
                mid = (min + max) / 2;
                string midVal = ar[mid].ToLower();
                int cmp = midVal.CompareTo(term);
                bool match = midVal.StartsWith(term);
                if (cmp == 0 || match)
                {
                    if(mid == 0 || !ar[mid - 1].ToLower().StartsWith(term))
                    {
                        return mid;
                    }
                    else
                    {
                        max = mid - 1;
                    }
                }
                else if (cmp < 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }
            return -1;
        }
        int BinarySearchMax(string[] ar, string term, int min, int max, int mid)
        {
            min = mid;
            while(min <= max)
            {
                mid = (min + max) / 2;
                string midVal = ar[mid].ToLower();
                int cmp = midVal.CompareTo(term);
                bool match = midVal.StartsWith(term);
                if (cmp == 0 || match)
                {
                    if(mid == ar.Length - 1 || !ar[mid + 1].ToLower().StartsWith(term))
                    {
                        return mid;
                    }
                    else
                    {
                        min = mid + 1;
                    }
                }
                else if (cmp < 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }
            return -1;
        }

        int[] BinarySearchByArtist(string artist)
        {
            // binary search, match beggining of string
            int min = 0;
            int max = _artistToSongIndices_artistIds.Length - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                string midVal = _artists[_artistToSongIndices_artistIds[mid]].ToLower();
                int cmp = midVal.CompareTo(artist);
                bool match = midVal.StartsWith(artist);
                if (cmp == 0 || match)
                {
                    int minIndex = BinarySearchByArtistMin(artist, min, max, mid);
                    int maxIndex = BinarySearchByArtistMax(artist, min, max, mid);
                    if(minIndex == -1 || maxIndex == -1)
                    {
                        return new int[]{0,0,0};
                    }
                    return new int[] { maxIndex - minIndex + 1, minIndex, maxIndex + 1 };
                }
                else if (cmp < 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }
            return new int[]{0,0,0};
        }

        int BinarySearchByArtistMin(string artist, int min, int max, int mid)
        {
            max = mid;
            while(min <= max)
            {
                mid = (min + max) / 2;
                string midVal = _artists[_artistToSongIndices_artistIds[mid]].ToLower();
                int cmp = midVal.CompareTo(artist);
                bool match = midVal.StartsWith(artist);
                if (cmp == 0 || match)
                {
                    if(mid == 0 || !_artists[_artistToSongIndices_artistIds[mid - 1]].ToLower().StartsWith(artist))
                    {
                        return mid;
                    }
                    else
                    {
                        max = mid - 1;
                    }
                }
                else if (cmp < 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }
            return -1;
        }

        int BinarySearchByArtistMax(string artist, int min, int max, int mid)
        {
            min = mid;
            while(min <= max)
            {
                mid = (min + max) / 2;
                string midVal = _artists[_artistToSongIndices_artistIds[mid]].ToLower();
                int cmp = midVal.CompareTo(artist);
                bool match = midVal.StartsWith(artist);
                if (cmp == 0 || match)
                {
                    if(mid == _artistToSongIndices_artistIds.Length - 1 || !_artists[_artistToSongIndices_artistIds[mid + 1]].ToLower().StartsWith(artist))
                    {
                        return mid;
                    }
                    else
                    {
                        min = mid + 1;
                    }
                }
                else if (cmp < 0)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }
            return -1;
        }

        public int[] LinearSearchSongContains(string term, int startIndex, int length, int maxResults)
        {
            term = term.ToLower();
            int[] results = new int[maxResults];
            int resultsCount = 0;
            for(int i = startIndex; i < startIndex + length && i < _names.Length; i++)
            {
                if(_names[i].ToLower().Contains(term))
                {
                    results[resultsCount] = i;
                    resultsCount++;
                    if(resultsCount == maxResults)
                    {
                        break;
                    }
                }
            }
            int[] results2 = new int[resultsCount];
            Array.Copy(results, results2, resultsCount);
            return results2;
        }

        public int GetSongIdFromAristIndices(int index)
        {
            return _artistToSongIndices_songIndices[index];
        }

        public string GetSongName(int index)
        {
            return _names[index];
        }

        public VRCUrl GetSongURL(int index)
        {
            return _urls[index];
        }

        public string GetSongArtist(int index)
        {
            return _artists[_artistIndices[index]];
        }

        public int GetSongLength(int index)
        {
            return _length[index];
        }

        public string GetSongLengthString(int index)
        {
            int length = _length[index];
            return length / 60 + ":" + (length % 60).ToString("00");
        }

        public int[] GetSongRelated(int index)
        {
            int count = 0;
            for(int i = index * 5; i < index * 5 + 5; i++)
            {
                if(_related[i] != -1)
                {
                    count++;
                }
            }
            int[] related = new int[count];
            Array.Copy(_related, index * 5, related, 0, count);
            return related;
        }

        public int GetRandomRelated(int index)
        {
            int count = 0;
            for(int i = index * 5; i < index * 5 + 5; i++)
            {
                if(_related[i] != -1)
                {
                    count++;
                }
            }
            if(count == 0)
            {
                return (index + 1) % _names.Length;
            }
            return _related[index * 5 + UnityEngine.Random.Range(0, count)];
        }

        public int GetRandomRealtedNotInList(int index, int[] list, int listLength)
        {
            int[] related = GetSongRelated(index);
            int start = UnityEngine.Random.Range(0, related.Length);
            for(int i = 0; i < related.Length; i++)
            {
                int j = (i + start) % related.Length;
                if(Array.IndexOf(list, related[j]) == -1)
                {
                    return related[j];
                }
            }
            return -1;
        }

        public string GetArtistName(int index)
        {
            return _artists[index];
        }

        public int GetSongCount()
        {
            return _names.Length;
        }

        public int GetArtistCount()
        {
            return _artists.Length;
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public YT_DB_Save Save()
        {
            YT_DB_Save save = ScriptableObject.CreateInstance<YT_DB_Save>();
            save.names = this._names;
            save.urls = this._urls;
            save.artistIndices = this._artistIndices;
            save.related = this._related;
            save.artists = this._artists;
            save.artistToSongIndices_artistIds = this._artistToSongIndices_artistIds;
            save.artistToSongIndices_songIndices = this._artistToSongIndices_songIndices;
            return save;
        }
        public void Load(YT_DB_Save save)
        {
            this._names = save.names;
            this._urls = save.urls;
            this._artistIndices = save.artistIndices;
            this._related = save.related;
            this._artists = save.artists;
            this._artistToSongIndices_artistIds = save.artistToSongIndices_artistIds;
            this._artistToSongIndices_songIndices = save.artistToSongIndices_songIndices;
        }

        public void SetDataArrays(string[] names, VRCUrl[] urls, int[] artistIndices, int[] related, string[] artists, ushort[] length)
        {
            _names = names;
            _urls = urls;
            _artists = artists;
            _artistIndices = artistIndices;
            _related = related;
            _length = length;
        }

        public void SetArtistToIndicesArrays(int[] artistIds, int[] songIndices)
        {
            _artistToSongIndices_artistIds = artistIds;
            _artistToSongIndices_songIndices = songIndices;
        }

        public void SetAssets(TextAsset songsAsset, TextAsset artistsAsset)
        {
            _songAsset = songsAsset;
            _artistAsset = artistsAsset;
        }

        public string[] GetNames()
        {
            return _names;
        }

        public VRCUrl[] GetUrls()
        {
            return _urls;
        }

        public int[] GetArtistIndices()
        {
            return _artistIndices;
        }

        public int[] GetRelated()
        {
            return _related;
        }

        public string[] GetArtists()
        {
            return _artists;
        }

        public int[] GetArtistToSongIndicesArtistIndices()
        {
            return _artistToSongIndices_artistIds;
        }

        public int[] GetArtistToSongIndicesSongIndices()
        {
            return _artistToSongIndices_songIndices;
        }

        public (string name, VRCUrl id, string artist, int[] related) GetSong(int index)
        {
            return (GetSongName(index), GetSongURL(index), GetSongArtist(index), GetSongRelated(index));
        }
#endif
    }
}