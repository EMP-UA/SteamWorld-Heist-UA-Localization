// =============================================================================
// SWH.LocEditor.Core — LanguageArchive.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Читає та записує мовні архіви гри (en.csv.z, en_dlcNN.csv.z) напряму —
//     без зовнішнього QuickBMS. Формат перевірено на реальному en.csv.z:
//     перші 4 байти — розмір розпакованого вмісту (little-endian), решта —
//     звичайний zlib-потік (RFC1950) над сирим CSV. .NET має вбудований
//     ZLibStream — жодних зовнішніх залежностей не потрібно.
//
//     Кожен запис на диск проходить само-перевірку: щойно стиснутий архів
//     одразу розпаковується назад у пам'яті й звіряється байт-у-байт із
//     вихідним вмістом. Якщо результат не збігається — виняток, і файл
//     на диску НЕ чіпається (запис через тимчасовий файл + атомарна заміна).
//
// EN: Reads and writes the game's language archives (en.csv.z,
//     en_dlcNN.csv.z) directly — no external QuickBMS involved. Format
//     confirmed on a real en.csv.z: the first 4 bytes are the decompressed
//     size (little-endian), the rest is a standard zlib stream (RFC1950)
//     wrapping the raw CSV. .NET's built-in ZLibStream needs no external
//     dependency.
//
//     Every write to disk is self-verified: the freshly compressed archive
//     is immediately decompressed back in memory and compared byte-for-byte
//     against the source content. If it doesn't match — an exception is
//     thrown and the file on disk is left untouched (write goes through a
//     temp file + atomic replace).
// =============================================================================

using System.IO.Compression;

namespace SWH.LocEditor.Core;

public static class LanguageArchive
{
    private const int SizeHeaderLength = 4;

    /// <summary>
    /// UA: True, якщо шлях вказує на стиснутий архів (.csv.z), а не на
    ///     звичайний .csv.
    /// EN: True if the path points to a compressed archive (.csv.z) rather
    ///     than a plain .csv.
    /// </summary>
    public static bool IsCompressedArchive(string filePath) =>
        filePath.EndsWith(".z", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// UA: Розпаковує вміст .csv.z. Перевіряє, що розмір після розпакування
    ///     збігається із заголовком — це рано ловить биті/обрізані файли.
    /// EN: Decompresses .csv.z content. Verifies the decompressed size
    ///     matches the header — catches corrupted/truncated files early.
    /// </summary>
    public static byte[] Decompress(byte[] archiveBytes)
    {
        if (archiveBytes.Length < SizeHeaderLength)
            throw new InvalidDataException(
                "UA: Файл закороткий для .csv.z (немає 4-байтного заголовка розміру). / " +
                "EN: File too short to be a .csv.z (missing 4-byte size header).");

        int declaredSize = BitConverter.ToInt32(archiveBytes, 0);

        using var input = new MemoryStream(archiveBytes, SizeHeaderLength, archiveBytes.Length - SizeHeaderLength);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        byte[] result = output.ToArray();

        if (result.Length != declaredSize)
            throw new InvalidDataException(
                $"UA: Розмір після розпакування не збігається із заголовком ({result.Length} != {declaredSize}) — " +
                "файл, можливо, пошкоджено. / " +
                $"EN: Decompressed size doesn't match the header ({result.Length} != {declaredSize}) — " +
                "the file may be corrupted.");

        return result;
    }

    /// <summary>
    /// UA: Стискає сирий CSV у формат .csv.z (4-байтний розмір + zlib).
    /// EN: Compresses raw CSV into the .csv.z format (4-byte size + zlib).
    /// </summary>
    public static byte[] Compress(byte[] rawCsvBytes)
    {
        using var output = new MemoryStream();
        output.Write(BitConverter.GetBytes(rawCsvBytes.Length), 0, SizeHeaderLength);
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(rawCsvBytes, 0, rawCsvBytes.Length);
        return output.ToArray();
    }

    /// <summary>
    /// UA: Стискає й одразу перевіряє результат само-декомпресією — щоб
    ///     ніколи не повернути биту .csv.z. Кидає виняток, якщо round-trip
    ///     не дав побайтово ідентичний вміст.
    /// EN: Compresses and immediately verifies the result via self-
    ///     decompression — so a broken .csv.z is never returned. Throws if
    ///     the round-trip doesn't produce byte-identical content.
    /// </summary>
    public static byte[] CompressAndVerify(byte[] rawCsvBytes)
    {
        byte[] archive = Compress(rawCsvBytes);
        byte[] verify = Decompress(archive);

        if (!verify.AsSpan().SequenceEqual(rawCsvBytes))
            throw new InvalidDataException(
                "UA: Самоперевірка після стиснення провалилась — файл НЕ записано. / " +
                "EN: Post-compression self-check failed — the file was NOT written.");

        return archive;
    }

    /// <summary>
    /// UA: Читає файл і повертає сирий CSV — автоматично розпаковуючи, якщо
    ///     розширення вказує на .csv.z, або читаючи як є, якщо це звичайний .csv.
    /// EN: Reads a file and returns raw CSV — auto-decompressing if the
    ///     extension indicates .csv.z, or reading as-is for a plain .csv.
    /// </summary>
    public static byte[] LoadRaw(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return IsCompressedArchive(path) ? Decompress(bytes) : bytes;
    }

    /// <summary>
    /// UA: Безпечний запис: стискає (з само-перевіркою, якщо шлях .csv.z),
    ///     пише у тимчасовий файл поруч і лише потім атомарно підміняє
    ///     цільовий файл. Якщо будь-де стається помилка — оригінал на диску
    ///     лишається недоторканим.
    /// EN: Safe write: compresses (with self-verification, if the path is
    ///     .csv.z), writes to a temp file next to it, and only then
    ///     atomically replaces the target file. If anything fails along the
    ///     way — the original file on disk is left untouched.
    /// </summary>
    public static void SaveRaw(string path, byte[] rawCsvBytes)
    {
        byte[] bytesToWrite = IsCompressedArchive(path)
            ? CompressAndVerify(rawCsvBytes)
            : rawCsvBytes;

        string tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, bytesToWrite);
        File.Move(tempPath, path, overwrite: true);
    }
}
