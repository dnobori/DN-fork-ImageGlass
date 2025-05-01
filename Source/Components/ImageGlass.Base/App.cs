/*
ImageGlass Project - Image viewer for Windows
Copyright (C) 2010 - 2024 DUONG DIEU PHAP
Project homepage: https://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace ImageGlass.Base;

public class App
{
    /// <summary>
    /// Gets the application executable path
    /// </summary>
    public static string IGExePath => StartUpDir("ImageGlass.exe");


    /// <summary>
    /// Gets the application name
    /// </summary>
    public static string AppName
    {
        get
        {
            var dt = BuildTimeStampUtil.GetThisAssemblyBuildDate();

            string timeStampStr = string.Format("{0:D4}.{1:D2}", dt.Year, dt.Month);

            return FileVersionInfo.GetVersionInfo(IGExePath).ProductName + "_DN " + timeStampStr;
        }
    }


    /// <summary>
    /// Gets the product version
    /// </summary>
    public static string Version => FileVersionInfo.GetVersionInfo(IGExePath).FileVersion ?? "";


    /// <summary>
    /// Checks if the current user is administator
    /// </summary>
    public static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent())
       .IsInRole(WindowsBuiltInRole.Administrator);


    /// <summary>
    /// Gets value of Portable mode if the startup dir is writable
    /// </summary>
    public static bool IsPortable => BHelper.CheckPathWritable(PathType.Dir, StartUpDir());


    /// <summary>
    /// Gets the path based on the startup folder of ImageGlass.
    /// </summary>
    public static string StartUpDir(params string[] paths)
    {
        var newPaths = paths.ToList();
        newPaths.Insert(0, Application.StartupPath);

        return Path.Combine([.. newPaths]);
    }


    /// <summary>
    /// Returns the path based on the configuration folder of ImageGlass.
    /// For portable mode, ConfigDir = InstalledDir, else <c>%LocalAppData%\ImageGlass</c>
    /// </summary>
    /// <param name="type">Indicates if the given path is either file or directory</param>
    public static string ConfigDir(PathType type, params string[] paths)
    {
        // use StartUp dir if it's writable
        var startUpDir = StartUpDir(paths);

        if (BHelper.CheckPathWritable(type, startUpDir))
        {
            return startUpDir;
        }

        // else, use AppData dir
        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

        // create the directory if not exists
        Directory.CreateDirectory(appDataDir);

        var newPaths = paths.ToList();
        newPaths.Insert(0, appDataDir);
        appDataDir = Path.Combine([.. newPaths]);

        return appDataDir;
    }



    /// <summary>
    /// Checks if ImageGlass starts with OS
    /// </summary>
    public static bool CheckStartWithOs()
    {
        const string APP_NAME = "ImageGlass";
        var regAppPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";


        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(regAppPath);
            var keyValue = key?.GetValue(APP_NAME)?.ToString();

            var isEnabled = !string.IsNullOrWhiteSpace(keyValue);

            return isEnabled;
        }
        catch { }

        return false;
    }


    /// <summary>
    /// Sets or unsets ImageGlass to start with OS in <see cref="IgCommands.STARTUP_BOOST"/> mode.
    /// Returns <c>null</c> if successful.
    /// </summary>
    public static Exception? SetStartWithOs(bool enable)
    {
        Exception? error = null;
        const string APP_NAME = "ImageGlass";
        var regAppPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(regAppPath, true);

            if (enable)
            {
                key?.SetValue(APP_NAME, $"\"{App.IGExePath}\" {IgCommands.STARTUP_BOOST}");
            }
            else
            {
                key?.DeleteValue(APP_NAME);
            }
        }
        catch (Exception ex) { error = ex; }


        return error;
    }


}

internal static class BuildTimeStampUtil
{
    public static DateTime StrToDateTime(string str, bool toUtc = false, bool emptyToZeroDateTime = false)
    {
        if (string.IsNullOrEmpty(str))
        {
            if (emptyToZeroDateTime) return new DateTime(0);
            return new DateTime(0);
        }
        DateTime ret = new DateTime(0);

        str = str.Trim();
        string[] sps =
            {
                    " ",
                    "_",
                    "　",
                    "\t",
                    "T",
                };

        string[] tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 2)
        {
            int r1 = str.IndexOf("年", StringComparison.OrdinalIgnoreCase);
            int r2 = str.IndexOf("月", StringComparison.OrdinalIgnoreCase);
            int r3 = str.IndexOf("日", StringComparison.OrdinalIgnoreCase);

            if (r1 != -1 && r2 != -1 && r3 != -1)
            {
                tokens = new string[2];

                tokens[0] = str.Substring(0, r3 + 1);
                tokens[1] = str.Substring(r3 + 1);
            }
        }

        if (tokens.Length == 2)
        {
            DateTime dt1 = StrToDate(tokens[0]);
            DateTime dt2 = StrToTime(tokens[1]);

            ret = dt1.Date + dt2.TimeOfDay;
        }
        else if (tokens.Length == 1)
        {
            if (tokens[0].Length == 14)
            {
                // yyyymmddhhmmss
                DateTime dt1 = StrToDate(tokens[0].Substring(0, 8));
                DateTime dt2 = StrToTime(tokens[0].Substring(8));

                ret = dt1.Date + dt2.TimeOfDay;
            }
            else if (tokens[0].Length == 12)
            {
                // yymmddhhmmss
                DateTime dt1 = StrToDate(tokens[0].Substring(0, 6));
                DateTime dt2 = StrToTime(tokens[0].Substring(6));

                ret = dt1.Date + dt2.TimeOfDay;
            }
            else
            {
                // 日付のみ
                DateTime dt1 = StrToDate(tokens[0]);

                ret = dt1.Date;
            }
        }
        else
        {
            throw new ArgumentException(str);
        }

        if (toUtc) ret = ret.ToUniversalTime();

        return ret;
    }

    // 文字列を int 型に変換する
    public static int StrToInt(string str)
    {
        try
        {
            str = str.Trim();
            str = str.Replace(",", "");
            if (int.TryParse(str, out int ret))
            {
                return ret;
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public static bool IsNumber(string str)
    {
        str = str.Trim();
        str = str.Replace(",", "");

        if (string.IsNullOrEmpty(str)) return false;

        foreach (char c in str)
        {
            if (IsNumber(c) == false)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsNumber(char c)
    {
        if (c >= '0' && c <= '9')
        {
        }
        else if (c == '-')
        {
        }
        else
        {
            return false;
        }

        return true;
    }

    public static DateTime StrToDate(string str, bool toUtc = false, bool emptyToZeroDateTime = false)
    {
        if (emptyToZeroDateTime && string.IsNullOrEmpty(str)) return new DateTime(0);

        string[] sps =
            {
                    "/",
                    "/",
                    "-",
                    ":",
                    "年",
                    "月",
                    "日",
                };
        str = str.Trim();
        //Str.NormalizeString(ref str, true, true, false, false);

        string[] youbi =
        {
                "月", "火", "水", "木", "金", "土", "日",
            };

        foreach (string ys in youbi)
        {
            string ys2 = string.Format("({0})", ys);

            str = str.Replace(ys2, "");
        }

        string[] tokens;

        DateTime ret = new DateTime(0);

        tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 3)
        {
            // yyyy/mm/dd
            string yearStr = tokens[0];
            string monthStr = tokens[1];
            string dayStr = tokens[2];
            int year = 0;
            int month = 0;
            int day = 0;

            if ((yearStr.Length == 1 || yearStr.Length == 2) && IsNumber(yearStr))
            {
                year = 2000 + StrToInt(yearStr);
            }
            else if (yearStr.Length == 4 && IsNumber(yearStr))
            {
                year = StrToInt(yearStr);
            }

            if ((monthStr.Length == 1 || monthStr.Length == 2) && IsNumber(monthStr))
            {
                month = StrToInt(monthStr);
            }
            if ((dayStr.Length == 1 || dayStr.Length == 2) && IsNumber(dayStr))
            {
                day = StrToInt(dayStr);
            }

            if (year < 1800 || year > 9000 || month <= 0 || month >= 13 || day <= 0 || day >= 32)
            {
                throw new ArgumentException(str);
            }

            ret = new DateTime(year, month, day);
        }
        else if (tokens.Length == 1)
        {
            if (str.Length == 8)
            {
                // yyyymmdd
                string yearStr = str.Substring(0, 4);
                string monthStr = str.Substring(4, 2);
                string dayStr = str.Substring(6, 2);
                int year = int.Parse(yearStr);
                int month = int.Parse(monthStr);
                int day = int.Parse(dayStr);

                if (year < 1800 || year > 9000 || month <= 0 || month >= 13 || day <= 0 || day >= 32)
                {
                    throw new ArgumentException(str);
                }

                ret = new DateTime(year, month, day);
            }
            else if (str.Length == 6)
            {
                // yymmdd
                string yearStr = str.Substring(0, 2);
                string monthStr = str.Substring(2, 2);
                string dayStr = str.Substring(4, 2);
                int year = int.Parse(yearStr) + 2000;
                int month = int.Parse(monthStr);
                int day = int.Parse(dayStr);

                if (year < 1800 || year > 9000 || month <= 0 || month >= 13 || day <= 0 || day >= 32)
                {
                    throw new ArgumentException(str);
                }

                ret = new DateTime(year, month, day);
            }
        }
        else
        {
            throw new ArgumentException(str);
        }

        if (toUtc)
        {
            ret = ret.ToUniversalTime();
        }

        return ret;
    }


    public static DateTime StrToTime(string str, bool toUtc = false, bool emptyToZeroDateTime = false)
    {
        if (emptyToZeroDateTime && string.IsNullOrEmpty(str)) return new DateTime(0);

        DateTime ret = new DateTime(0);

        string[] sps =
            {
                    "/",
                    "-",
                    ":",
                    "時",
                    "分",
                    "秒",
                };

        str = str.Trim();

        string[] tokens;

        tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 3)
        {
            // hh:mm:ss
            string hourStr = tokens[0];
            string minuteStr = tokens[1];
            string secondStr = tokens[2];
            string msecStr = "";
            int hour = -1;
            int minute = -1;
            int second = -1;
            int msecond = 0;
            long add_ticks = 0;

#pragma warning disable CA1310 // 正確さのために StringComparison を指定する
            int msec_index = secondStr.IndexOf(".");
#pragma warning restore CA1310 // 正確さのために StringComparison を指定する
            if (msec_index != -1)
            {
                msecStr = secondStr.Substring(msec_index + 1);
                secondStr = secondStr.Substring(0, msec_index);

                msecStr = "0." + msecStr;

                decimal tmp = decimal.Parse(msecStr);
                msecond = (int)((tmp % 1.0m) * 1000.0m);
                add_ticks = (int)((tmp % 0.001m) * 10000000.0m);
            }

            if ((hourStr.Length == 1 || hourStr.Length == 2) && IsNumber(hourStr))
            {
                hour = StrToInt(hourStr);
            }
            if ((minuteStr.Length == 1 || minuteStr.Length == 2) && IsNumber(minuteStr))
            {
                minute = StrToInt(minuteStr);
            }
            if ((secondStr.Length == 1 || secondStr.Length == 2) && IsNumber(secondStr))
            {
                second = StrToInt(secondStr);
            }

            if (hour < 0 || hour >= 25 || minute < 0 || minute >= 60 || second < 0 || second >= 60 || msecond < 0 || msecond >= 1000)
            {
                throw new ArgumentException(str);
            }

            ret = new DateTime(2000, 1, 1, hour, minute, second, msecond).AddTicks(add_ticks);
        }
        else if (tokens.Length == 2)
        {
            // hh:mm
            string hourStr = tokens[0];
            string minuteStr = tokens[1];
            int hour = -1;
            int minute = -1;
            int second = 0;

            if ((hourStr.Length == 1 || hourStr.Length == 2) && IsNumber(hourStr))
            {
                hour = StrToInt(hourStr);
            }
            if ((minuteStr.Length == 1 || minuteStr.Length == 2) && IsNumber(minuteStr))
            {
                minute = StrToInt(minuteStr);
            }

            if (hour < 0 || hour >= 25 || minute < 0 || minute >= 60 || second < 0 || second >= 60)
            {
                throw new ArgumentException(str);
            }

            ret = new DateTime(2000, 1, 1, hour, minute, second);
        }
        else if (tokens.Length == 1)
        {
            string hourStr = tokens[0];
            int hour = -1;
            int minute = 0;
            int second = 0;
            int msec = 0;

            if ((hourStr.Length == 1 || hourStr.Length == 2) && IsNumber(hourStr))
            {
                // hh
                hour = StrToInt(hourStr);
            }
            else
            {
                if ((hourStr.Length == 4) && IsNumber(hourStr))
                {
                    // hhmm
                    int i = StrToInt(hourStr);
                    hour = i / 100;
                    minute = i % 100;
                }
                else if ((hourStr.Length == 6) && IsNumber(hourStr))
                {
                    // hhmmss
                    int i = StrToInt(hourStr);
                    hour = i / 10000;
                    minute = ((i % 10000) / 100);
                    second = i % 100;
                }
                else if ((hourStr.Length == 10 && hourStr[6] == '.'))
                {
                    // hhmmss.abc
                    int i = StrToInt(hourStr.Substring(0, 6));
                    hour = i / 10000;
                    minute = ((i % 10000) / 100);
                    second = i % 100;

                    msec = StrToInt(hourStr.Substring(7));
                }
            }

            if (hour < 0 || hour >= 25 || minute < 0 || minute >= 60 || second < 0 || second >= 60 || msec < 0 || msec >= 1000)
            {
                throw new ArgumentException(str);
            }

            ret = new DateTime(2000, 1, 1, hour, minute, second, msec);
        }
        else
        {
            throw new ArgumentException(str);
        }

        if (toUtc)
        {
            ret = ret.ToUniversalTime();
        }

        return ret;
    }

    static DateTime GetThisAssemblyBuildDateInternal()
    {
        return GetAssemblyBuildDate(typeof(BuildTimeStampUtil).Assembly);
    }

    public static DateTime GetAssemblyBuildDate(Assembly assembly)
    {
        // Thanks to: https://www.meziantou.net/getting-the-date-of-build-of-a-dotnet-assembly-at-runtime.htm

        try
        {
            const string BuildVersionMetadataPrefix = "+build";

            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
#pragma warning disable CA1310 // 正確さのために StringComparison を指定する
                var index = value.IndexOf(BuildVersionMetadataPrefix);
#pragma warning restore CA1310 // 正確さのために StringComparison を指定する
                if (index > 0)
                {
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);

                    DateTime dt = StrToDateTime(value, emptyToZeroDateTime: true);

                    return dt;
                }
            }
        }
        catch { }

        return default;
    }

    static DateTime? cached;

    public static DateTime GetThisAssemblyBuildDate()
    {
        if (cached == null)
        {
            cached = GetThisAssemblyBuildDateInternal();
        }

        return cached.Value;
    }
}

