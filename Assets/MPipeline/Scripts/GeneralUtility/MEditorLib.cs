#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
namespace MPipeline
{
    public unsafe static class MEditorLib
    {
        public static AssetReference SetObjectAddressable(Object go, string guid)
        {
            AddressableAssetSettings aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = aaSettings.DefaultGroup;
            AddressableAssetEntry entry = aaSettings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = aaSettings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            }
            return new AssetReference(guid);

        }

        public static AssetReference SetObjectAddressable(Object go)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go));
            AddressableAssetSettings aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = aaSettings.DefaultGroup;
            AddressableAssetEntry entry = aaSettings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = aaSettings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            }
            return new AssetReference(guid);
        }

        public static Texture2D GetTexFromRT(RenderTexture rt)
        {
            Texture2D newTex = new Texture2D(rt.width, rt.height, rt.graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
            RenderTexture.active = rt;
            newTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            return newTex;
        }

        public static bool GenerateWorldCreatorTexture(RenderTexture albedo, RenderTexture normal, string savePath, string pictureName)
        {
            if (!Directory.Exists(savePath)) return false;
            if (!File.Exists(savePath + "Description.xml")) return false;
            RenderTexture albedoThumb = new RenderTexture(albedo.descriptor);
            RenderTexture normalThumb = new RenderTexture(normal.descriptor);
            albedoThumb.width = 256;
            albedoThumb.height = 256;
            normalThumb.width = 256;
            normalThumb.height = 256;
            albedoThumb.Create();
            normalThumb.Create();
            Graphics.Blit(albedo, albedoThumb);
            Graphics.Blit(normal, normalThumb);

            Texture2D albedoTex = GetTexFromRT(albedo);
            Texture2D normalTex = GetTexFromRT(normal);
            Texture2D albedoTexThumb = GetTexFromRT(albedoThumb);
            Texture2D normalTexThumb = GetTexFromRT(normalThumb);
            string albedoName = pictureName + "_basecolor.png";
            string normalName = pictureName + "_normal.png";
            string albedoNameThumb = pictureName + "_basecolor_thumb.png";
            string normalNameThumb = pictureName + "_normal_thumb.png";
            File.WriteAllBytes(savePath + albedoName, albedoTex.EncodeToPNG());
            File.WriteAllBytes(savePath + normalName, normalTex.EncodeToPNG());
            File.WriteAllBytes(savePath + albedoNameThumb, albedoTexThumb.EncodeToPNG());
            File.WriteAllBytes(savePath + normalNameThumb, normalTexThumb.EncodeToPNG());
            StreamReader sr = new StreamReader(savePath + "Description.xml");
            string backup = "";
            for (string s = sr.ReadLine(); !string.IsNullOrEmpty(s); s = sr.ReadLine())
            {
                if (s.Contains("Diffuse"))
                {

                   s = "    <Diffuse File=\"" + albedoName + "\" Time=\"131734223420000000\" />";
                }
                if (s.Contains("Normal"))
                {
                    s = "    <Normal File=\"" + normalName + "\" Time=\"131734223420000000\" />";
                }
                backup += s + "\n";
            }
            sr.Dispose();
            StreamWriter sw = new StreamWriter(savePath + "Description.xml ");
            sw.Write(backup);
            sw.Dispose();
            Object.DestroyImmediate(albedoTex);
            Object.DestroyImmediate(albedoTexThumb);
            Object.DestroyImmediate(normalTex);
            Object.DestroyImmediate(normalTexThumb);
            Object.DestroyImmediate(albedoThumb);
            Object.DestroyImmediate(normalThumb);
            return true;
        }
    }
}
#endif