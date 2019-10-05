using System.Linq;
using UnityEngine;
using System.IO;

namespace UnityEditor.Experimental.Rendering
{
    static class PostProcessShaderIncludePath
    {
        public static string[] GetPaths()
        {
            var srpMarker = Directory.GetFiles(Application.dataPath, "POSTFXMARKER", SearchOption.AllDirectories).FirstOrDefault();
            var paths = new string[srpMarker == null ? 1 : 2];
            var index = 0;
            if (srpMarker != null)
            {
                paths[index] = Directory.GetParent(srpMarker).ToString();
                index++;
            }
            paths[index] = Path.GetFullPath("Packages/com.unity.postprocessing");
            return paths;
        }
    }
}
