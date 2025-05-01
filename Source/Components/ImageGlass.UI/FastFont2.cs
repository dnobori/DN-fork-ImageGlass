using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ImageGlass.UI;

public static class FastFont2
{
    static readonly string WinFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
    static readonly string UserFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");

    static readonly PrivateFontCollection Col = new PrivateFontCollection();
    static readonly HashSet<string> FilenameHashSet = new HashSet<string>();

    static readonly Dictionary<string, FontFamily> NameToFontFamilyDict = new Dictionary<string, FontFamily>();
    static readonly Dictionary<string, Font> CachedFont = new Dictionary<string, Font>();

    static bool ResetFlag = true;

    static List<int> LangIDList = new List<int>();

    static FastFont2()
    {
        LangIDList.Add(0);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("en-us").LCID);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("ja-jp").LCID);

        // For system-wide dialog fonts
        LoadFontFile("msgothic.ttc");
        LoadFontFile("YuGothM.ttc");
        LoadFontFile("meiryo.ttc");
    }

    //public static Font GetDefaultFont() => GetCachedFont("Meiryo UI");
    public static Font GetDefaultFont() => GetCachedFont("Meiryo UI");

    public static void LoadFontFile(string fontFileName)
    {
        lock (FilenameHashSet)
        {
            if (FilenameHashSet.Contains(fontFileName) == false)
            {
                string fontPath1 = Path.Combine(WinFontsDirPath, fontFileName);
                string fontPath2 = Path.Combine(UserFontsDirPath, fontFileName);

                bool ok = false;

                try
                {
                    if (File.Exists(fontPath1))
                    {
                        Col.AddFontFile(fontPath1);
                        ok = true;
                    }
                    else if (File.Exists(fontPath2))
                    {
                        Col.AddFontFile(fontPath2);
                        ok = true;
                    }
                }
                catch
                {
                }

                if (ok)
                {
                    FilenameHashSet.Add(fontFileName);

                    lock (NameToFontFamilyDict)
                    {
                        ResetFlag = true;
                    }
                }
            }
        }
    }
    static void RebuildNameToFontFamilyDictIfNecessary()
    {
        lock (NameToFontFamilyDict)
        {
            if (ResetFlag)
            {
                NameToFontFamilyDict.Clear();

                foreach (var family in Col.Families)
                {
                    foreach (var lang in LangIDList)
                    {
                        string name = family.GetName(lang);

                        NameToFontFamilyDict[name] = family;
                    }
                }

                ResetFlag = false;
            }
        }
    }

    public static Font CreateFont(string fontName, float fontSize = 9.0f, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        RebuildNameToFontFamilyDictIfNecessary();

        if (NameToFontFamilyDict.TryGetValue(fontName, out var family) == false)
        {
            if (NameToFontFamilyDict.TryGetValue("MS UI Gothic", out var family2) == false)
            {
#pragma warning disable CA2201 // 予約された例外の種類を発生させません
                throw new ApplicationException($"Font name '{fontName}' not found.");
#pragma warning restore CA2201 // 予約された例外の種類を発生させません
            }

            family = family2;
        }

        return new Font(family, fontSize, style, unit);
    }

    public static Font GetCachedFont(string fontName, float fontSize = 9.0f, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        string key = $"{fontName}:{fontSize}:{style}:{unit}";

        lock (CachedFont)
        {
            if (CachedFont.TryGetValue(key, out var ret))
            {
                return ret;
            }
        }

        var ret2 = CreateFont(fontName, fontSize, style, unit);

        lock (CachedFont)
        {
            CachedFont[key] = ret2;
        }

        return ret2;
    }

    public static void SetFormsDefaultFontForBootSpeedUp(Font? font = null)
    {
#pragma warning disable IDISP001 // Dispose created
        if (font == null) font = GetDefaultFont();
#pragma warning restore IDISP001 // Dispose created

        Application.SetDefaultFont(font);

        {
            Type targetType = typeof(System.Windows.Forms.Control);

            FieldInfo? fieldInfo = targetType.GetField(
                "defaultFont",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(null, font);
            }
        }

        {
            Type targetType = typeof(System.Windows.Forms.ToolStripManager);

            FieldInfo? fieldInfo = targetType.GetField(
                "defaultFont",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(null, font);
            }
        }

        {
            Type targetType = typeof(System.Windows.Forms.ToolStripManager);

            FieldInfo? fieldInfo = targetType.GetField(
                "s_defaultFontCache",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            PropertyInfo? propertyInfo = targetType.GetProperty(
                "CurrentDpi",
                 BindingFlags.NonPublic | BindingFlags.Static);

            int currentDpi = -1;

            if (propertyInfo != null)
            {
                currentDpi = (int)propertyInfo.GetValue(null)!;
            }

            if (currentDpi != -1)
            {
                if (fieldInfo != null)
                {
                    ConcurrentDictionary<int, Font>? s_defaultFontCache = (ConcurrentDictionary<int, Font>?)fieldInfo.GetValue(null);

                    if (s_defaultFontCache != null)
                    {
                        s_defaultFontCache[currentDpi] = font;
                    }
                }
            }
        }
    }
}
