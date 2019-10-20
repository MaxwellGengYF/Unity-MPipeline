#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;
namespace MPipeline
{
    public static class MEditorLib
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

        public static AssetReference  SetObjectAddressable(Object go)
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
    }
}
#endif