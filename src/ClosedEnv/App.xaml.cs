using System.Diagnostics;
using System.Windows;
using ClosedEnv.Services;

namespace ClosedEnv;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        AppPaths.EnsureLayout();
        AppPaths.SyncScripts();

        var profileId = ParseProfileId(e.Args);
        if (profileId is null)
        {
            new MainWindow().Show();
            return;
        }

        var profile = ProfileStore.Load()
            .FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            var message = string.Equals(profileId, "max-web", StringComparison.OrdinalIgnoreCase)
                ? "Профиль MAX Web не загрузился. Нужен вшитый max-web. Пересоберите exe через scripts\\publish.ps1."
                : $"Профиль «{profileId}» не найден.";
            MessageBox.Show(message, "ClosedEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        if (profile.RequiresPayload)
        {
            MessageBox.Show(
                "Для Generic нужен файл. Откройте лаунчер и укажите .exe / .msi.",
                "ClosedEnv",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            new MainWindow().Show();
            return;
        }

        try
        {
            ProfileSession.Start(profile, camera: profile.VideoInput, audio: profile.AudioInput);
            if (!profile.IsWeb)
            {
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Не удалось открыть веб-MAX:\n" + ex.Message,
                "ClosedEnv",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
        }
    }

    private static string? ParseProfileId(string[] args)
    {
        var processName = Process.GetCurrentProcess().ProcessName;
        if (processName.StartsWith("ClosedEnv-Web", StringComparison.OrdinalIgnoreCase))
        {
            return "max-web";
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--web", StringComparison.OrdinalIgnoreCase))
            {
                return "max-web";
            }

            if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
