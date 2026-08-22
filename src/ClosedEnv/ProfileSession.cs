using System.Windows;
using ClosedEnv.Models;
using ClosedEnv.Services;

namespace ClosedEnv;

internal static class ProfileSession
{
    public static void Start(
        AppProfile profile,
        bool camera = false,
        bool audio = false,
        bool clipboard = false,
        string? payload = null,
        Window? owner = null)
    {
        if (profile.IsWeb)
        {
            var web = new WebWindow(profile, camera, audio);
            if (owner is not null)
            {
                web.Owner = owner;
            }

            web.Show();
            return;
        }

        var options = new SessionOptions
        {
            Networking = profile.Networking,
            AudioInput = audio,
            VideoInput = camera,
            Clipboard = clipboard,
            ProtectedClient = profile.ProtectedClient,
            MemoryMb = profile.MemoryMb > 0 ? profile.MemoryMb : 4096
        };
        SandboxLauncher.Launch(profile, options, payload);
    }
}
