using System.Runtime.InteropServices;
using System.Text;

namespace ReelForge.Native;

internal static class NativeEngine
{
    [DllImport("ReelForge.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    public static extern int RF_RenderTimeline(
        string ffmpegPath,
        string manifestPath,
        string outputPath,
        int width,
        int height,
        int fps,
        string audioPath,
        StringBuilder errorBuffer,
        int errorBufferChars);

    [DllImport("ReelForge.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    public static extern int RF_CopyFileFast(string sourcePath, string destinationPath, StringBuilder errorBuffer, int errorBufferChars);

    public static string GetError(StringBuilder buffer)
        => buffer.ToString().TrimEnd('\0', '\r', '\n');
}
