using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
public class SearchText : ScriptableWizard
{
    public string folderPath;
    public string extent = "cginc";
    public string targetText;
    [MenuItem("MPipeline/Search Text")]
    private static void CreateWizard()
    {
        DisplayWizard<SearchText>("Search", "Print");
    }
    private void OnWizardCreate()
    {
        foreach (string file in Directory.EnumerateFiles(folderPath, "*." + extent))
        {
            string contents = File.ReadAllText(file);
            if (contents.Contains(targetText))
                Debug.Log(file);
        }
    }
}