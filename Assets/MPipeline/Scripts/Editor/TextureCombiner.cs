using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class TextureCombiner : EditorWindow
{
    [MenuItem("MPipeline/Combine Texture")]
    private static void CreateWizard()
    {
        TextureCombiner window = (TextureCombiner)GetWindow(typeof(TextureCombiner));
        window.Show();
    }
    public enum ChannelUsage
    {
        R, G, B, A
    };
    public bool reverseR = false;
    public Texture2D r;
    public ChannelUsage rTextureChannel;
    public Texture2D g;
    public ChannelUsage gTextureChannel;
    public Texture2D b;
    public ChannelUsage bTextureChannel;
    public Texture2D a;
    public ChannelUsage aTextureChannel;
    public bool srgb = false;
    public static string fileName = "_SMO";

    private float GetColor(ChannelUsage usage, Color c)
    {
        switch (usage)
        {
            case ChannelUsage.A:
                return c.a;
            case ChannelUsage.R:
                return c.r;
            case ChannelUsage.B:
                return c.b;
            case ChannelUsage.G:
                return c.g;
        }
        return 0;
    }

    private void OnGUI()
    {
        string path;
        Vector2Int size;
        if (r)
        {
            path = AssetDatabase.GetAssetPath(r) + "_" + fileName + ".png";
            size = new Vector2Int(r.width, r.height);
        }
        else if (g)
        {
            path = AssetDatabase.GetAssetPath(g) + "_" + fileName + ".png";
            size = new Vector2Int(g.width, g.height);
        }
        else if (b)
        {
            path = AssetDatabase.GetAssetPath(b) + "_" + fileName + ".png";
            size = new Vector2Int(b.width, b.height);
        }
        else if (a)
        {
            path = AssetDatabase.GetAssetPath(a) + "_" + fileName + ".png";
            size = new Vector2Int(a.width, a.height);
        }
        else
        {
            path = "Assets/" + fileName + ".png";
            size = new Vector2Int(1,1);
        }
        r = EditorGUILayout.ObjectField("R Channel Texture: ", r, typeof(Texture2D), false) as Texture2D;
        rTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("R Texture Channel: ", rTextureChannel);
        g = EditorGUILayout.ObjectField("G Channel Texture: ", g, typeof(Texture2D), false) as Texture2D;
        gTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("G Texture Channel: ", gTextureChannel);
        b = EditorGUILayout.ObjectField("B Channel Texture: ", b, typeof(Texture2D), false) as Texture2D;
        bTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("B Texture Channel: ", bTextureChannel);
        a = EditorGUILayout.ObjectField("A Channel Texture: ", a, typeof(Texture2D), false) as Texture2D;
        aTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("A Texture Channel: ", aTextureChannel);
        reverseR = EditorGUILayout.Toggle("Reverse R Channel", reverseR);
        srgb = EditorGUILayout.Toggle("SRGB", srgb);
        if (GUILayout.Button("Combine Texture"))
        {
            if (r && !r.isReadable)
            {
                Debug.LogError("You have to set R channel Texture as readable!");
                return;
            }
            if (g && !g.isReadable)
            {
                Debug.LogError("You have to set G channel Texture as readable!");
                return;
            }
            if (b && !b.isReadable)
            {
                Debug.LogError("You have to set B channel Texture as readable!");
                return;
            }
            if (a && !a.isReadable)
            {
                Debug.LogError("You have to set A channel Texture as readable!");
                return;
            }
            Texture2D resultTex = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false, !srgb);
            Color[] colors = new Color[size.x * size.y];
            for (int x = 0; x < size.x; ++x)
            {
                for (int y = 0; y < size.y; ++y)
                {
                    Color cl = new Color
                    {
                        r = r ? GetColor(rTextureChannel, r.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 1,
                        g = g ? GetColor(gTextureChannel, g.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 1,
                        b = b ? GetColor(bTextureChannel, b.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 1,
                        a = a ? GetColor(aTextureChannel, a.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 1
                    };
                    colors[y * size.x + x] = cl;
                }
            }
            if (reverseR)
            {
                for (int i = 0; i < colors.Length; ++i)
                {
                    colors[i].r = 1 - colors[i].r;
                }
            }
            resultTex.SetPixels(colors);
            resultTex.Apply();
            
            System.IO.File.WriteAllBytes(path, resultTex.EncodeToPNG());

        }
    }
}