namespace InsightMovie.VoiceVox;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Manages the lifecycle of a local VOICEVOX engine process.
/// Handles finding the engine executable, launching it, and stopping it.
/// </summary>
public class EngineLauncher : IDisposable
{
    private const int STARTUP_TIMEOUT_SECONDS = 10;
    private const int KILL_WAIT_MILLISECONDS = 5000;

    private string? _enginePath;
    private Process? _process;

    /// <summary>
    /// Creates a new EngineLauncher.
    /// </summary>
    /// <param name="enginePath">
    /// Optional path to the VOICEVOX engine executable.
    /// If not provided, <see cref="FindDefaultEnginePath"/> will be used to locate it.
    /// </param>
    public EngineLauncher(string? enginePath = null)
    {
        _enginePath = enginePath;
    }

    /// <summary>
    /// Gets or sets the path to the VOICEVOX engine executable.
    /// </summary>
    public string? EnginePath
    {
        get => _enginePath;
        set => _enginePath = value;
    }

    /// <summary>
    /// Returns true if the engine process is currently running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            if (_process == null)
                return false;

            try
            {
                return !_process.HasExited;
            }
            catch (InvalidOperationException)
            {
                // Process object is not associated with a running process
                return false;
            }
        }
    }

    /// <summary>
    /// Searches well-known locations for the VOICEVOX engine executable.
    /// Search order:
    /// 1. %LOCALAPPDATA%\InsightMovie\voicevox\run.exe
    /// 2. Relative path from the application directory: voicevox\run.exe
    /// 3. %ProgramFiles%\VOICEVOX\run.exe
    /// </summary>
    /// <returns>The full path to the engine executable, or null if not found.</returns>
    public static string? FindDefaultEnginePath()
    {
        // 1. Application directory: voicevox\run.exe
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var appRelative = Path.Combine(appDir, "voicevox", "run.exe");
        if (File.Exists(appRelative))
            return appRelative;

        // 2. LOCALAPPDATA\InsightMovie\voicevox\run.exe
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var localPath = Path.Combine(localAppData, "InsightMovie", "voicevox", "run.exe");
            if (File.Exists(localPath))
                return localPath;
        }

        // 3. Standard VOICEVOX installation paths
        string[] searchPaths =
        {
            Path.Combine(localAppData ?? "", "Programs", "VOICEVOX", "vv-engine", "run.exe"),
            Path.Combine(localAppData ?? "", "Programs", "VOICEVOX ENGINE", "run.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "VOICEVOX", "vv-engine", "run.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "VOICEVOX ENGINE", "run.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "VOICEVOX", "run.exe"),
        };

        foreach (var path in searchPaths)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Launches the VOICEVOX engine process and waits for it to become ready.
    /// </summary>
    /// <param name="port">The port for the engine to listen on. Defaults to 50021.</param>
    /// <param name="useGpu">Whether to enable GPU acceleration. Defaults to true.</param>
    /// <returns>True if the engine started and became reachable; false otherwise.</returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the engine executable cannot be found.
    /// </exception>
    public async Task<bool> Launch(int port = 50021, bool useGpu = true)
    {
        if (IsRunning)
            return true;

        var path = _enginePath ?? FindDefaultEnginePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            throw new FileNotFoundException(
                "VOICEVOX engine executable not found. " +
                "Please specify the engine path or install VOICEVOX.",
                path ?? "run.exe");
        }

        var arguments = $"--port {port}";
        if (useGpu)
        {
            arguments += " --use_gpu";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _process = new Process { StartInfo = startInfo };

        try
        {
            _process.Start();

            // Discard stdout/stderr to prevent buffer deadlocks
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _process.Dispose();
            _process = null;
            throw new InvalidOperationException(
                $"Failed to start VOICEVOX engine: {ex.Message}", ex);
        }

        // Wait for the engine to become reachable
        using var client = new VoiceVoxClient($"http://{VoiceVoxClient.DEFAULT_HOST}:{port}");
        var deadline = DateTime.UtcNow.AddSeconds(STARTUP_TIMEOUT_SECONDS);

        while (DateTime.UtcNow < deadline)
        {
            if (!IsRunning)
                return false;

            var version = await client.CheckConnectionAsync();
            if (version != null)
                return true;

            await Task.Delay(500);
        }

        return false;
    }

    /// <summary>
    /// Stops the running VOICEVOX engine process.
    /// First attempts a graceful termination via CloseMainWindow, then falls back to Kill.
    /// </summary>
    public void Stop()
    {
        if (_process == null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                // Attempt graceful shutdown first
                _process.CloseMainWindow();

                if (!_process.WaitForExit(KILL_WAIT_MILLISECONDS))
                {
                    // Force kill if graceful shutdown fails
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(KILL_WAIT_MILLISECONDS);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited
        }
        catch (Exception)
        {
            // Best effort cleanup - swallow errors during shutdown
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    /// <summary>
    /// Stops and relaunches the VOICEVOX engine process.
    /// </summary>
    /// <param name="port">The port for the engine to listen on. Defaults to 50021.</param>
    /// <param name="useGpu">Whether to enable GPU acceleration. Defaults to true.</param>
    /// <returns>True if the engine restarted and became reachable; false otherwise.</returns>
    public async Task<bool> Restart(int port = 50021, bool useGpu = true)
    {
        Stop();
        // Brief pause to allow port release
        await Task.Delay(1000);
        return await Launch(port, useGpu);
    }

    /// <summary>
    /// Releases resources and stops the engine process if running.
    /// </summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
