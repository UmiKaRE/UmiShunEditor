using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace UmiShun.Utils;


/// <summary>
/// CREDIT: https://www.codeproject.com/Tips/5300748/A-Few-Missing-Methods-on-BinaryReader
/// Provides some conspicuously absent string and type functionality to 
/// <seealso cref="BinaryReader"/>
/// </summary>
static class BinaryReaderExtensions
{
    /// <summary>
    /// Reads a C style null terminated ASCII string
    /// </summary>
    /// <param name="reader">The binary reader</param>
    /// <returns>A string as read from the stream</returns>
    public static string ReadSZString(this BinaryReader reader)
    {
        var result = new StringBuilder();
        while (true)
        {
            byte b = reader.ReadByte();
            if (0 == b)
                break;
            result.Append((char)b);
        }
        return result.ToString();
    }
    /// <summary>
    /// Reads a fixed size ASCII string
    /// </summary>
    /// <param name="reader">The binary reader</param>
    /// <param name="count">The number of characters</param>
    /// <returns>A string as read from the stream</returns>
    public static string ReadFixedString(this BinaryReader reader,int count)
    {
        return Encoding.ASCII.GetString(reader.ReadBytes(count));
    }
}