
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;
using System.Linq;
using System.IO;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UdonSharpEditor;
using System.Collections.Generic;
#endif

namespace Thry.YTDB
{
    public class YT_DB_Manager : UdonSharpBehaviour
    {
        public YT_DB Database;
    }

    
#if UNITY_EDITOR && !COMPILER_UDONSHARP 

    class Song
    {
        public string sortName;
        public string sortArtist;
        public string name;
        public string artists;
        public string artist;
        public string id;
        public string[] related;
        public int index;
        public ushort length;
        public int views;
    }

    [CustomEditor(typeof(YT_DB_Manager))]
    public class YT_DB_Editor : Editor {
        TextAsset _input;
        string _testSearch;
        bool _isProduction;
        YT_DB_Save _save;
        const string SAVE_PATH = "Assets/Thry/YT_DB/YT_DB_Save.asset";

        YT_DB NewCleanDatabase(YT_DB_Manager manager)
        {
            if(manager.Database != null)
            {
                GameObject.DestroyImmediate(manager.Database.gameObject);
            }
            GameObject dbObj = new GameObject("[DO NOT SELECT] YT_DB");
            dbObj.transform.parent = manager.transform;
            dbObj.transform.localPosition = Vector3.zero;
            dbObj.transform.localRotation = Quaternion.identity;
            dbObj.transform.localScale = Vector3.one;
            YT_DB db  = UdonSharpUndo.AddComponent<YT_DB>(dbObj);
            manager.Database = db;
            UdonSharpEditorUtility.CopyProxyToUdon(manager);
            EditorUtility.SetDirty(manager.Database);
            return db;
        }

        static TextAsset ArrayToTextAsset(string[] ar, string path)
        {
            string write = string.Join("\n", ar);
            File.WriteAllText(path, write);
            AssetDatabase.ImportAsset(path);
            return (TextAsset)AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset));
        }

        public override void OnInspectorGUI() {
            UdonSharpEditor.UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target);
            UdonSharpEditor.UdonSharpGUI.DrawVariables(target);

            serializedObject.Update();
            YT_DB_Manager manager = (YT_DB_Manager)target;
            YT_DB db = manager.Database;

            EditorGUILayout.Space(10);

            // property field for text file
            _input = (TextAsset) EditorGUILayout.ObjectField("Input", _input, typeof(TextAsset), false);
            _isProduction = db != null && db.GetNames() != null && db.GetNames().Length > 0;
            if(_save == null)
                _save = AssetDatabase.LoadAssetAtPath<YT_DB_Save>(SAVE_PATH);

            if(GUILayout.Button("Clear"))
            {
                db = NewCleanDatabase(manager);

                UdonSharpEditorUtility.CopyProxyToUdon(db);
                serializedObject.ApplyModifiedProperties();

                EditorUtility.SetDirty(db);
            }

            if(GUILayout.Button("Import")) 
            {
                // parse the text file
                string[] lines = _input.text.Split('\n');
                Song[] songs = new Song[lines.Length];
                HashSet<string> songsIds = new HashSet<string>();
                HashSet<string> artistSet = new HashSet<string>();
                for(int i = 0; i < lines.Length; i++) {
                    if(i % 100 == 0)
                        EditorUtility.DisplayProgressBar("YT_DB", "Parsing", (float)i / lines.Length);
                    songs[i] = JsonUtility.FromJson<Song>(lines[i]);
                    songsIds.Add(songs[i].id);
                    songs[i].artist = songs[i].artists.Split(',')[0];
                    // No idea why this happens
                    if(string.IsNullOrWhiteSpace(songs[i].artist))
                        songs[i].artist = "Unknown";
                    songs[i].sortName = songs[i].name.ToLower();
                    songs[i].sortArtist = songs[i].artist.ToLower();
                    // artists
                    if(!artistSet.Contains(songs[i].artist))
                    {
                        artistSet.Add(songs[i].artist);
                    }
                }

                // Limit related to 5 and make sure they are valid
                for(int i = 0; i < songs.Length; i++) {
                    if(i % 100 == 0)
                        EditorUtility.DisplayProgressBar("YT_DB", "Validating", (float)i / lines.Length);
                    List<string> relatedIds = new List<string>();
                    foreach(string id in songs[i].related) {
                        if(songsIds.Contains(id) && relatedIds.Count < 5)
                            relatedIds.Add(id);
                    }
                    songs[i].related = relatedIds.ToArray();
                }

                EditorUtility.DisplayProgressBar("YT_DB", "Sorting", 0);
                // sort by name
                System.Array.Sort(songs, (a, b) => a.sortName.CompareTo(b.sortName));

                // create arrays
                string[] names = new string[songs.Length];
                VRCUrl[] urls = new VRCUrl[songs.Length];
                int[] artistIndices = new int[songs.Length];
                ushort[] lengths = new ushort[songs.Length];
                int[] related = new int[songs.Length * 5];
                string[] artists = new string[artistSet.Count];

                Dictionary<string, int> artistToIndex = new Dictionary<string, int>();
                Dictionary<string, int> idToIndex = new Dictionary<string, int>();

                // sort artists by name
                artistSet.CopyTo(artists);
                System.Array.Sort(artists, (a, b) => a.ToLower().CompareTo(b.ToLower()));
                for(int i = 0; i < artists.Length; i++) 
                {
                    artistToIndex.Add(artists[i], i);
                }

                // fill id to index
                for(int i = 0; i < songs.Length; i++) {
                    if(idToIndex.ContainsKey(songs[i].id))
                    {
                        Debug.LogError("Duplicate id: " + songs[i].id);
                    }else
                    {
                        idToIndex.Add(songs[i].id, i);
                    }
                }

                for(int i = 0; i < songs.Length; i++) {
                    if(i % 100 == 0)
                        EditorUtility.DisplayProgressBar("YT_DB", "Creating arrays", (float)i / songs.Length);
                    names[i] = songs[i].name;
                    lengths[i] = (ushort)songs[i].length;
                    // this is a bullshit vrc requirement. urls dont work without the https://
                    urls[i] = new VRCUrl( "https://youtu.be/" + songs[i].id );
                    artistIndices[i] = artistToIndex[songs[i].artist];
                    // translate realted ids to indices
                    int j = 0;
                    foreach(string relatedId in songs[i].related) {
                        if(idToIndex.ContainsKey(relatedId)) {
                            related[i * 5 + j] = idToIndex[relatedId];
                            j++;
                            if(j >= 5)
                                break;
                        }
                    }
                    for(; j < 5; j++) {
                        related[i * 5 + j] = -1;
                    }
                    songs[i].index = i; // used for sorting by artist
                }

                // set data arrays
                db.SetDataArrays(names, urls, artistIndices, related, artists.ToArray(), lengths);
                db.SetAssets(ArrayToTextAsset(names, "Assets/Thry/YT_DB/Names.txt"), ArrayToTextAsset(artists.ToArray(), "Assets/Thry/YT_DB/Artists.txt"));

                EditorUtility.DisplayProgressBar("YT_DB", "Sorting", 0);
                // sort by artist
                System.Array.Sort(songs, (a, b) => a.sortArtist.CompareTo(b.sortArtist));

                // create artist to indices
                int[] artistToSongIndices_artistIndices = new int[songs.Length];
                int[] artistToSongIndices_songIndices = new int[songs.Length];
                for(int i = 0; i < songs.Length; i++) {
                    if(i % 100 == 0)
                        EditorUtility.DisplayProgressBar("YT_DB", "Creating artist to indices", (float)i / songs.Length);
                    artistToSongIndices_artistIndices[i] = artistToIndex[songs[i].artist];
                    artistToSongIndices_songIndices[i] = songs[i].index;
                }

                // set artist to indices
                db.SetArtistToIndicesArrays(artistToSongIndices_artistIndices, artistToSongIndices_songIndices);

                EditorUtility.ClearProgressBar();

                UdonSharpEditorUtility.CopyProxyToUdon(db);
                //ClearDB(db); // should only clear proxy
                serializedObject.ApplyModifiedProperties();

                EditorUtility.SetDirty(db);
            }
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Set to 'Active' before publishing to VRChat. Set to 'Stash' when working on scene to improve save / upload time.", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            Color backgroundColor = GUI.backgroundColor;
            if(_isProduction) GUI.backgroundColor = Color.green;
            if(GUILayout.Button("Active", GUILayout.Height(70)) && !_isProduction && _save != null)
            {
                _isProduction = true;
                // copy data from manager.Saved to db
                db.Load(_save);
            }
            GUI.backgroundColor = backgroundColor;
            if(!_isProduction) GUI.backgroundColor = Color.green;
            if(GUILayout.Button("Stash", GUILayout.Height(70)) && _isProduction)
            {
                _isProduction = false;
                Undo.RecordObjects(new UnityEngine.Object[] { db, manager }, "Stash");
                // write db to file
                AssetDatabase.CreateAsset(db.Save(), SAVE_PATH);
                AssetDatabase.SaveAssets();
                // clear db
                NewCleanDatabase(manager);
            }
            GUI.backgroundColor = backgroundColor;
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.Space(50);

            // test search
            _testSearch = EditorGUILayout.TextField("Test Search", _testSearch);
            if(GUILayout.Button("Search song by name"))
            {
                db.LoadAssets();
                int[] countMinMax = db.SearchByName(_testSearch);
                Debug.Log("Found " + countMinMax[0] + " songs");
                for(int i = countMinMax[1]; i < countMinMax[2]; i++)
                {
                    (string name, VRCUrl url, string artist, int[] related) = db.GetSong(i);
                    Debug.Log("Song " + i + ": " + name + " by " + artist + " (" + url + ")");
                }
            }

            if(GUILayout.Button("Search song by artist"))
            {
                db.LoadAssets();
                int[] countMinMax = db.SearchByArtist(_testSearch);
                Debug.Log("Found " + countMinMax[0] + " songs");
                for(int i = countMinMax[1]; i < countMinMax[2]; i++)
                {
                    (string name, VRCUrl url, string artist, int[] related) = db.GetSong(i);
                    Debug.Log("Song " + i + ": " + name + " by " + artist + " (" + url + ")");
                }
            }

            if(GUILayout.Button("Output sizes of array"))
            {
                // output size in format 1,000,000,000,000
                Debug.Log("Names: " + ByteCountOfArray(db.GetNames()).ToString("#,#") );
                Debug.Log("Urls: " + ByteCountOfArray(db.GetUrls()).ToString("#,#") );
                Debug.Log("Artist Indices: " + ByteCountOfArray(db.GetArtistIndices()).ToString("#,#") );
                Debug.Log("Related: " + ByteCountOfArray(db.GetRelated()).ToString("#,#") );
                Debug.Log("Artists: " + ByteCountOfArray(db.GetArtists()).ToString("#,#") );
                Debug.Log("Artist to Song Indices Artist Indices: " + ByteCountOfArray(db.GetArtistToSongIndicesArtistIndices()).ToString("#,#") );
                Debug.Log("Artist to Song Indices Song Indices: " + ByteCountOfArray(db.GetArtistToSongIndicesSongIndices()).ToString("#,#") );
            }
                
        }

        static float ByteCountOfArray(Array array)
        {
            // if array is string[], we need to count the length of each string
            if(array.GetType().GetElementType() == typeof(string))
            {
                int totalLength = 0;
                foreach(string s in array)
                {
                    totalLength += s.Length;
                }
                return totalLength * 2;
            }
            if(array.GetType().GetElementType() == typeof(VRCUrl))
            {
                int totalLength = 0;
                foreach(VRCUrl s in array)
                {
                    totalLength += s.Get().Length;
                }
                return totalLength * 2;
            }
            return array.Length * System.Runtime.InteropServices.Marshal.SizeOf(array.GetType().GetElementType());
        }
    }
#endif
}
