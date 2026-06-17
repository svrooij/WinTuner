using OpenMcdf;
using System.Text;

namespace WingetIntune.Msi;
/// <summary>
/// Cross-platform MSI Decoder using OpenMcdf
/// </summary>
/// <remarks>Created by <see href="https://github.com/miyoyo">miyoyo</see> in <see href="https://github.com/svrooij/WingetIntune/pull/154">PR 154</see></remarks>
public class MsiDecoder
{
    private int stringSize = 2;
    private Dictionary<uint, string> intToString;
    private string[] tables;
    private List<Dictionary<string, object>> columns;
    private Dictionary<string, List<Dictionary<string, object>>> allTables;

    const int MSITYPE_VALID = 0x0100;
    // const int MSITYPE_LOCALIZABLE = 0x200;
    const int MSITYPE_STRING = 0x0800;
    const int MSITYPE_NULLABLE = 0x1000;
    const int MSITYPE_KEY = 0x2000;
    // const int MSITYPE_TEMPORARY = 0x4000;
    // const int MSITYPE_UNKNOWN = 0x8000;

    public MsiDecoder(string filePath)
    {
        try
        {
            using (var cf = new CompoundFile(filePath))
            {
                load(cf);
            }
        }
        catch (CFFileFormatException e)
        {
            throw new InvalidDataException($"MSI Parsing error: Attempted to parse the MSI file at {filePath}, but the file was corrupt.", e);
        }
    }

    public MsiDecoder(Stream msiStream)
    {
        try
        {
            using (var cf = new CompoundFile(msiStream))
            {
                load(cf);
            }
        }
        catch (CFFileFormatException e)
        {
            throw new InvalidDataException($"MSI Parsing error: Attempted to parse an MSI Stream, but the data was corrupt.", e);
        }
    }

    /// <summary>
    /// Get the product code of the MSI file (including the braces)
    /// </summary>
    /// <returns></returns>
    public string? GetCode() => GetStringValue("Property", "ProductCode");

    /// <summary>
    /// Get the version of the MSI file
    /// </summary>
    /// <returns></returns>
    public string? GetVersion() => GetStringValue("Property", "ProductVersion");

    /// <summary>
    /// Get the value of a property in a table
    /// </summary>
    /// <param name="table">MSI details are stored in the "Property" table</param>
    /// <param name="property"></param>
    /// <param name="value">You probably want the "Value", don't know what other options you have.</param>
    /// <returns></returns>
    public string? GetStringValue(string table, string property, string value = "Value")
    {
        if (!allTables.ContainsKey(table))
        {
            return null;
        }

        return allTables[table].Where(row => (string)row["Property"] == property).Select<Dictionary<string, object>, string>(row => row[value].ToString()).FirstOrDefault();
    }

    private void load(CompoundFile cf)
    {
        try
        {
            intToString = LoadStringPool(cf);

            tables = LoadTablesTable(cf);
            columns = LoadColumns(cf);
            allTables = LoadAllTables(cf);
        }
        catch (CFItemNotFound e)
        {
            throw new InvalidDataException("MSI Parsing error: A stream was being looked for in the MSI file, but was not found. Either the MSI file is corrupt, or the library used to read the MSI file is broken.", e);
        }
        catch (InvalidDataException)
        {
            throw;

        }
        catch (Exception e)
        {
            throw new InvalidDataException("MSI Parsing error: A generic error was encountered while parsing the MSI file.", e);
        }
    }

    // references for the next lines:
    // https://stackoverflow.com/questions/9734978/view-msi-strings-in-binary

    private char BaseMSIDecode(char c)
    {
        // 0-0x3F converted to '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz._'
        // all other values higher as 0x3F converted also to '_'

        int result;

        if (c < 10)
            result = c + '0';             // 0-9 (0x0-0x9) -> '0123456789'
        else if (c < (10 + 26))
            result = c - 10 + 'A';        // 10-35 (0xA-0x23) -> 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
        else if (c < (10 + 26 + 26))
            result = c - 10 - 26 + 'a';   // 36-61 (0x24-0x3D) -> 'abcdefghijklmnopqrstuvwxyz'
        else if (c == (10 + 26 + 26))       // 62 (0x3E) -> '.'
            result = '.';
        else
            result = '_';                 // 63-0xffffffff (0x3F-0xFFFFFFFF) -> '_'

        return (char)result;
    }

    private char BaseMSIEncode(char c)
    {
        // only '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz._' are allowed and converted to 0-0x3F

        int result;


        if ((c >= '0') && (c <= '9'))   // '0123456789' -> 0-9  (0x0-0x9)
            result = c - '0';
        else if ((c >= 'A') && (c <= 'Z'))   // 'ABCDEFGHIJKLMNOPQRSTUVWXYZ' -> 10-35 (26 chars) - (0xA-0x23)
            result = c - 'A' + 10;
        else if ((c >= 'a') && (c <= 'z'))   // 'abcdefghijklmnopqrstuvwxyz' -> 36-61 (26 chars) - (0x24-0x3D)
            result = c - 'a' + 10 + 26;
        else if (c == '.')
            result = 10 + 26 + 26;        // '.' -> 62 (0x3E)
        else if (c == '_')
            result = 10 + 26 + 26 + 1;      // '_' -> 63 (0x3F) - 6 bits
        else
            result = -1; // other -> -1 (0xFF)
        return (char)result;
    }

    private string EncodeStreamName(string name)
    {
        var result = new List<char>();

        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '$')
            {
                result.Add((char)0x4840);
            }
            else if (c < 0x80 && BaseMSIEncode(c) <= 0x3F && i + 1 != name.Length)
            {
                i++;
                var first = BaseMSIEncode(c);
                var second = BaseMSIEncode(name[i]);
                result.Add((char)(first + (second << 6) + 0x3800));
            }
            else
            {
                result.Add((char)(BaseMSIEncode(c) + 0x4800));
            }

        }

        return new string(result.ToArray());
    }

    private bool IsTypeBinary(int type) => (type & (~MSITYPE_NULLABLE)) == (MSITYPE_STRING | MSITYPE_VALID);

    private Dictionary<uint, string> LoadStringPool(CompoundFile cf)
    {
        var decodedStringPool = EncodeStreamName("$_StringPool");
        var streamStringPool = cf.RootStorage.GetStream(decodedStringPool);
        var stringPoolBytes = streamStringPool.GetData();
        var poolLength = streamStringPool.Size;
        var poolWLength = BitConverter.ToUInt16(stringPoolBytes, 0);
        var poolRefCount = BitConverter.ToUInt16(stringPoolBytes, 2);

        stringSize = poolRefCount switch
        {
            0 => 2,
            0x8000 => 3,
            _ => throw new InvalidDataException($"MSI Parsing error: attempted to read the expected size of strings from the string pool and got {poolRefCount}, but only 0 and 0x8000 (32768) are allowed.")
        };

        var decodedStringData = EncodeStreamName("$_StringData");
        var streamStringData = cf.RootStorage.GetStream(decodedStringData);
        var stringDataBytes = streamStringData.GetData();

        var strings = new Dictionary<uint, string>();

        uint stringId = 1;
        for (int src = 4, offset = 0; src < poolLength; src += 4)
        {
            var entryLength = (int)BitConverter.ToUInt16(stringPoolBytes, src);
            var entryRef = (int)BitConverter.ToUInt16(stringPoolBytes, src + 2);

            if (entryLength == 0 && entryRef == 0)
            {
                // Empty entry, skip.
                strings.Add(stringId, "");
                stringId++;
                continue;
            }
            else if (entryLength == 0 && entryRef != 0)
            {
                // wide entry over 64kb
                continue;
            }

            if (src != 4)
            {

                var previousEntryLength = BitConverter.ToInt16(stringPoolBytes, src - 4);
                var previousEntryRef = BitConverter.ToInt16(stringPoolBytes, src - 2);

                if (previousEntryLength == 0 && previousEntryRef != 0)
                {
                    entryLength = (entryLength) + (entryRef << 16);
                }
            }

            if (offset + entryLength > stringDataBytes.Length)
            {
                throw new InvalidDataException($"MSI Parsing error: attempted to read too many bytes while parsing the string table. {stringDataBytes.Length} are available, but {offset + entryLength} were needed.");
            }

            strings.Add(stringId, Encoding.UTF8.GetString(stringDataBytes.Skip(offset).Take(entryLength).ToArray()));
            offset += entryLength;
            stringId++;
        }

        strings[0] = "";

        return strings;
    }

    private List<Dictionary<string, object>> LoadColumns(CompoundFile cf)
    {
        var encodedColumnName = EncodeStreamName("$_Columns");
        var columnStream = cf.RootStorage.GetStream(encodedColumnName);
        var columnBytes = columnStream.GetData();

        string[] columnTitles =
        {
            "Table",
            "Number",
            "Name",
            "Type"
        };

        int[] columnTypes =
        {
            MSITYPE_VALID | MSITYPE_STRING | MSITYPE_KEY | 64,
            MSITYPE_VALID | MSITYPE_KEY | 2,
            MSITYPE_VALID | MSITYPE_STRING | 64,
            MSITYPE_VALID | 2,
        };

        return ParseTable(columnBytes, columnTitles, columnTypes);
    }

    private string[] LoadTablesTable(CompoundFile cf)
    {
        var encodedColumnName = EncodeStreamName("$_Tables");
        var tableStream = cf.RootStorage.GetStream(encodedColumnName);
        var tableBytes = tableStream.GetData();

        string[] tableTitles =
        {
            "Name"
        };

        int[] tableTypes =
        {
             MSITYPE_VALID | MSITYPE_STRING | MSITYPE_KEY | 64
        };

        var results = ParseTable(tableBytes, tableTitles, tableTypes);
        var output = results.Select<Dictionary<string, object>, string>(x => x["Name"].ToString()).ToArray<string>();

        return output;
    }

    private Dictionary<string, List<Dictionary<string, object>>> LoadAllTables(CompoundFile cf)
    {
        var results = new Dictionary<string, List<Dictionary<string, object>>>();
        foreach (var table in tables)
        {
            CFStream? tableStream = null;
            try
            {
                tableStream = cf.RootStorage.GetStream(EncodeStreamName($"${table}"));
            }
            catch (CFItemNotFound)
            { }
            if (tableStream == null)
            {
                try
                {
                    tableStream = cf.RootStorage.GetStream(EncodeStreamName(table));
                }
                catch (CFItemNotFound)
                { }
            }
            if (tableStream == null)
            {
                try
                {
                    tableStream = cf.RootStorage.GetStream(table);
                }
                catch (CFItemNotFound)
                {
                    // The table is empty, ignore.
                    results[table] = new List<Dictionary<string, object>>();
                    continue;
                }
            }
            var tableBytes = tableStream.GetData();
            var columnTitles = columns.Where(row => (string)row["Table"] == table).Select<Dictionary<string, object>, string>(row => row["Name"].ToString()).ToArray();
            var columnTypes = columns.Where(row => (string)row["Table"] == table).Select<Dictionary<string, object>, int>(row => (int)row["Type"]).ToArray();

            results[table] = ParseTable(tableBytes, columnTitles, columnTypes);
        }

        return results;
    }

    // I would use List<Dictionary<string, string|int>> but this ain't F#
    private List<Dictionary<string, object>> ParseTable(byte[] tableBytes, string[] columnTitles, int[] columnTypes)
    {
        var columnCount = columnTitles.Length;
        var rowSize = GetRowSize(columnTypes);
        var rowCount = tableBytes.Length / rowSize;

        if (tableBytes.Length % rowSize != 0)
        {
            throw new InvalidDataException($"MSI Parsing error: attempted to compute the amount of rows of a table, but the computed row size ({rowSize}) is not divisible by the table's byte count ({tableBytes.Length}).");
        }

        var output = new List<Dictionary<string, object>>();

        var offset = 0;
        for (var h = 0; h < columnTitles.Length; h++)
        {
            for (var i = 0; i < rowCount; i++)
            {
                if (h == 0)
                {
                    output.Add(new Dictionary<string, object>());
                }
                if (IsTypeBinary(columnTypes[h]))
                {
                    output[i][columnTitles[h]] = "Stub Value";
                }
                else if ((columnTypes[h] & MSITYPE_STRING) != 0)
                {
                    var read = ReadString(tableBytes, offset);
                    offset += read.Item2;
                    output[i][columnTitles[h]] = read.Item1;
                }
                else
                {
                    var read = ReadNumber(tableBytes, offset, columnTypes[h] & 0xFF);
                    offset += read.Item2;
                    output[i][columnTitles[h]] = read.Item1;
                }
            }
        }

        return output;
    }

    private int GetRowSize(int[] columnTypes)
    {
        int size = 0;

        foreach (var columnType in columnTypes)
        {
            if (IsTypeBinary(columnType))
            {
                size += 2;
            }
            else if ((columnType & MSITYPE_STRING) != 0)
            {
                size += stringSize;
            }
            else
            {
                var columnSize = (columnType & 0xFF);
                size += columnSize switch
                {
                    1 => 2,
                    2 => 2,
                    3 => 4,
                    4 => 4,
                    _ => throw new InvalidDataException($"MSI Parsing error: attempted to add a column of size {columnSize}, but only 1, 2, 3 and 4 are supported.")
                };
            }
        }

        return size;
    }

    // Read a string, and return both the string, as well as the offset
    private (string, int) ReadString(byte[] data, int index)
    {
        var stringRef = stringSize switch
        {
            2 => BitConverter.ToUInt16(data, index),
            3 => BitConverter.ToUInt16(data, index) + (uint)(data[index + 2] << 16),
            4 => BitConverter.ToUInt32(data, index),
            _ => throw new InvalidDataException($"MSI Parsing error: attempted to parse a string index of size {stringSize}, but only 2, 3 and 4 are supported.")
        };

        return (intToString[stringRef], stringSize);
    }

    // Read an int, and return both the int and the offset
    private (int, int) ReadNumber(byte[] data, int index, int bytes)
    {
        bytes = bytes switch
        {
            1 => 2,
            2 => 2,
            3 => 4,
            4 => 4,
            _ => throw new InvalidDataException($"MSI Parsing error: Attempted to parse an integer of length {bytes}, but 1, 2, 3 and 4 are supported.")
        };

        if (index + bytes > data.Length)
        {
            throw new InvalidDataException($"MSI Parsing error: Attempted to parse an integer of (adjusted) length {bytes}, but not enough bytes are left to parse.");
        }

        int ret = 0, i;

        for (i = 0; i < bytes; i++)
            ret += (int)(data[index + i]) << i * 8;

        if (bytes == 2)
        {
            ret = ret - 0x8000;
        }
        else
        {
            ret = (int)((long)ret - 0x80000000);
        }

        return (ret, bytes);
    }

}
