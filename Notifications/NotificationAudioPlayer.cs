using System.Runtime.InteropServices;

namespace CrescentAtlas.Notifications;

public static partial class NotificationAudioPlayer
{
    private const uint SoundAsync = 0x0001;
    private const uint SoundNoDefault = 0x0002;
    private const uint SoundFileName = 0x00020000;

    public static bool TryPlayFile(string path)
        => System.IO.File.Exists(path)
           && PlaySound(
               path,
               IntPtr.Zero,
               SoundAsync | SoundNoDefault | SoundFileName);

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(
        string sound,
        IntPtr module,
        uint flags);
}
