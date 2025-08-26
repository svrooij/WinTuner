using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WingetIntune.Models;
/// <summary>
/// a comparer for version strings that can handle both valid version formats and fall back to string comparison
/// </summary>
public class StringVersionComparer : Comparer<string>
{
    public override int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        try
        {
            var versionx = new Version(x);
            var versiony = new Version(y);
            return versionx.CompareTo(versiony);
        }
        catch (Exception) //FormatException
        {
            // If the version strings are not in a valid format, we can fall back to a string comparison

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
