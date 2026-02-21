namespace InsightMovie;

using System;
using System.Windows;
using InsightMovie.Core;
using InsightMovie.Services;
using InsightMovie.Video;
using InsightMovie.Views;
using InsightMovie.VoiceVox;

/// <summary>
/// Application entry point. Handles first-run setup, engine discovery,
/// FFmpeg initialisation, and main window creation.
/// </summary>
public partial class App : Application
{
    private VoiceVoxClient? _voiceVoxClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global unhandled exception handlers to prevent silent crashes
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                LocalizationService.GetString("App.Error.Unexpected", args.Exception.Message),
                LocalizationService.GetString("App.Error.Unexpected.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    LocalizationService.GetString("App.Error.Fatal", ex.Message),
                    LocalizationService.GetString("App.Error.Fatal.Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        try
        {
            await StartupAsync(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.GetString("App.Error.Startup", ex.Message),
                LocalizationService.GetString("App.Error.Startup.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task StartupAsync(StartupEventArgs e)
    {

        // ── 1. Load configuration ──────────────────────────────────
        var config = new Config();

        // ── 1.5. Initialize localization ─────────────────────────────
        LocalizationService.Initialize(config.Language);

        if (config.LoadFailed)
        {
            MessageBox.Show(
                LocalizationService.GetString("App.Config.Corrupted"),
                LocalizationService.GetString("App.Config.Corrupted.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // ── 2. First-run setup wizard ──────────────────────────────
        VoiceVoxClient? wizardClient = null;

        if (config.IsFirstRun)
        {
            var wizard = new SetupWizard();
            var result = wizard.ShowDialog();

            if (result == true)
            {
                // Persist the values chosen in the wizard.
                // The wizard creates its own VoiceVoxClient internally;
                // retrieve the connected client and speaker ID.
                wizardClient = wizard.GetClient();
                var wizardSpeakerId = wizard.GetSpeakerId();

                config.BeginUpdate();
                if (wizardClient != null)
                    config.EngineUrl = wizardClient.BaseUrl;
                if (wizardSpeakerId >= 0)
                    config.DefaultSpeakerId = wizardSpeakerId;
                config.IsFirstRun = false;
                config.EndUpdate();
            }
            else
            {
                // User cancelled -- nothing to do, shut down.
                Shutdown();
                return;
            }
        }

        // ── 3. Create VOICEVOX client ──────────────────────────────
        // Reuse the client from the wizard when available; otherwise create a new one.
        var client = wizardClient ?? new VoiceVoxClient(config.EngineUrl);
        _voiceVoxClient = client;

        if (!config.IsFirstRun)
        {
            // Quick connection check; try auto-discovery on failure.
            var version = await client.CheckConnectionAsync();
            if (version == null)
            {
                var discovered = await client.DiscoverEngineAsync();
                if (discovered != null)
                {
                    config.BeginUpdate();
                    config.EngineUrl = discovered.BaseUrl;
                    config.EndUpdate();
                }
            }
        }

        // ── 4. FFmpeg wrapper ──────────────────────────────────────
        FFmpegWrapper? ffmpeg = null;
        try
        {
            ffmpeg = new FFmpegWrapper();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                LocalizationService.GetString("App.FFmpeg.Error", ex.Message),
                "InsightCast",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // ── 5. Default speaker ID ──────────────────────────────────
        int speakerId = config.DefaultSpeakerId ?? 13;

        // ── 6. Show quick mode (default) or main window ─────────────
        var quickMode = new QuickModeWindow(client, speakerId, ffmpeg, config);
        quickMode.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _voiceVoxClient?.Dispose();
        base.OnExit(e);
    }
}
