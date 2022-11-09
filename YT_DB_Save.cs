using VRC.SDKBase;
using UnityEngine;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

namespace Thry.YTDB
{
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public class YT_DB_Save : ScriptableObject
    {
        public string[] names;
        public VRCUrl[] urls;
        public int[] artistIndices;
        public int[] related;
        public string[] artists;
        public int[] artistToSongIndices_artistIds;
        public int[] artistToSongIndices_songIndices;
    }
#endif
}