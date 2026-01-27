namespace InsightMovie;

using System;
using System.Windows;
using InsightMovie.Core;
using InsightMovie.Video;
using InsightMovie.Views;
using InsightMovie.VoiceVox;

/// <summary>
/// Application entry point. Handles first-run setup, engine discovery,
/// FFmpeg initialisation, and main window creation.
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global unhandled exception handlers to prevent silent crashes
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"予期しないエラーが発生しました。\n\n{args.Exception.Message}\n\n" +
                "アプリケーションの動作が不安定になる可能性があります。",
                "InsightMovie - エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"致命的なエラーが発生しました。\n\n{ex.Message}",
                    "InsightMovie - 致命的エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        // ── 1. Load configuration ──────────────────────────────────
        var config = new Config();

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

        if (!config.IsFirstRun)
        {
            // Quick connection check; try auto-discovery on failure.
            var version = await client.CheckConnectionAsync();
            if (version == null)
            {
                var discovered = await client.DiscoverEngineAsync();
                if (discovered != null)
                {
                    config.EngineUrl = discovered.BaseUrl;
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
                $"FFmpeg の初期化に失敗しました。\n動画生成機能は使用できません。\n\n{ex.Message}\n\n" +
                "以下のいずれかの方法でFFmpegを配置してください:\n" +
                "• PATH環境変数にffmpeg.exeのあるフォルダを追加\n" +
                "• アプリフォルダ内に tools\\ffmpeg\\bin\\ffmpeg.exe を配置\n" +
                "• build.ps1 を実行して自動ダウンロード",
                "InsightMovie",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // ── 5. Default speaker ID ──────────────────────────────────
        int speakerId = config.DefaultSpeakerId ?? 13;

        // ── 6. Show main window ────────────────────────────────────
        var mainWindow = new MainWindow(client, speakerId, ffmpeg, config);
        mainWindow.Show();
    }
}
