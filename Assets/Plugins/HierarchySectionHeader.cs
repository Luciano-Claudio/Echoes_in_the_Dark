using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using System;
using System.Globalization;

//Colocar esse script dentro de uma pasta Editor

[InitializeOnLoad]
public static class HierarchySectionHeader
{
    [Obsolete]
    static HierarchySectionHeader()
    {
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
    }

    [Obsolete]
    static void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
    {
        var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (gameObject == null) return;
        var name = gameObject.name;
        if (!name.StartsWith("//", System.StringComparison.Ordinal)) return;
        Regex r = new Regex("#([a-f0-9]{2})([a-f0-9]{2})([a-f0-9]{2})", RegexOptions.IgnoreCase);
        Match m = r.Match(name);
        Color blockColor = Color.black;

        if (m.Success)
        {
            var hexRedColor = m.Groups[1];
            var hexGreenColor = m.Groups[2];
            var hexBlueColor = m.Groups[3];
            float redColor = Byte.Parse(hexRedColor.ToString(), NumberStyles.HexNumber);
            float greenColor = Byte.Parse(hexGreenColor.ToString(), NumberStyles.HexNumber);
            float blueColor = Byte.Parse(hexBlueColor.ToString(), NumberStyles.HexNumber);

            blockColor = new Color(redColor / 255, greenColor / 255, blueColor / 255);
            name = Regex.Replace(name, "#([a-f0-9]{2})([a-f0-9]{2})([a-f0-9]{2})", "", RegexOptions.IgnoreCase);
        }

        EditorGUI.DrawRect(selectionRect, blockColor);
        EditorGUI.DropShadowLabel(selectionRect, name.Replace("/", "").Trim().ToUpperInvariant());
    }
}