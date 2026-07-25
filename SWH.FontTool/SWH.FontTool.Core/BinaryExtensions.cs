// =============================================================================
// SWH.FontTool.Core — BinaryExtensions.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Хелпери для читання/запису float у "переставленому" (word-swapped,
//     CDAB) байтовому порядку — залишок від ранніх ітерацій реверс-аналізу,
//     коли ще не було ясно, чи всі поля .fnt зберігаються в звичайному
//     little-endian. ПРИМІТКА: на момент цього узгодження формату жоден
//     клас проєкту ці методи не викликає (FontAnalyzer/FontGenerator
//     використовують звичайний BitConverter/BinaryPrimitives, little-endian
//     підтверджено бінарним аналізом). Залишено про запас — не видаляти
//     без перевірки, чи не знадобиться для якогось нестандартного шрифту.
// EN: Helpers for reading/writing floats in "word-swapped" (CDAB) byte
//     order — a leftover from early reverse-engineering passes, back when
//     it wasn't yet clear whether every .fnt field used plain little-endian.
//     NOTE: as of this formatting pass, no class in the project calls these
//     methods (FontAnalyzer/FontGenerator use plain BitConverter/
//     BinaryPrimitives — little-endian has been confirmed by binary
//     analysis). Left in place just in case — don't remove without
//     checking whether some non-standard font might still need it.
// =============================================================================

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SWH.FontTool.Core;

public static class BinaryExtensions
{
    // UA: Читання Word-Swapped Float (CDAB)
    // EN: Reads a word-swapped (CDAB) float
    public static float ReadBigEndianSingle(this BinaryReader reader)
    {
        byte[] b = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian) Array.Reverse(b); // UA: Перевертаємо, якщо система Little Endian / EN: Reverse if the system is little-endian
        return BitConverter.ToSingle(b, 0);
    }

    public static void WriteSwappedSingle(this BinaryWriter writer, float value)
    {
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();
        writer.Write(new byte[] { bytes[2], bytes[3], bytes[0], bytes[1] });
    }
}