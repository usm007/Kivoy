using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Kivoy.Models;
using Kivoy.ViewModels;

namespace Kivoy.Services;

public sealed class DownloadManager
{
    private readonly ToastService _toast;
    private bool _pauseAll;

    public DownloadManager(ToastService toast)
    {
        _toast = toast;
        History = HistoryStore.Load();
    }

    public ObservableCollection<DownloadJob> ActiveJobs { get; } = new();

    public List<HistoryEntry> History { get; }

    public int MaxConcurrent => SettingsStore.Instance.MaxConcurrent;

    public bool IsPausedAll => _pauseAll;

    public event EventHandler<DownloadJob>? JobCompleted;

    public event EventHandler? LoginRequiredDetected;

    public void Enqueue(IEnumerable<(MediaItem Item, string? SubFolder)> entries, DownloadOptions opts)
    {
        foreach (var (item, subFolder) in entries)
        {
            var folder = string.IsNullOrWhiteSpace(subFolder)
                ? opts.DestinationFolder
                : Path.Combine(opts.DestinationFolder, subFolder);

            var job = new DownloadJob(item, opts, this, folder);
            ActiveJobs.Add(job);
        }

        TryStartNext();
    }

    public void TryStartNextPublic() => TryStartNext();

    private void TryStartNext()
    {
        if (_pauseAll)
            return;
        while (CountRunning() < MaxConcurrent)
        {
            var next = ActiveJobs.FirstOrDefault(j => j.State == JobState.Queued);
            if (next is null)
                break;

            next.State = JobState.Downloading;
            _ = RunJobAsync(next);
        }
    }

    private int CountRunning() =>
        ActiveJobs.Count(j => j.State is JobState.Downloading or JobState.Processing);

    private async Task RunJobAsync(DownloadJob job)
    {
        var progress = new Progress<JobProgress>(job.ApplyProgress);
        using var cts = new CancellationTokenSource();
        job.AttachRun(cts);

        string? error = null;
        var completed = false;

        try
        {
            var psi = YtDlpRunner.CreateInfo(BuildArgs(job));
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                error = "Failed to start yt-dlp.";
            }
            else
            {
                job.Proc = proc;
                var errTail = new StringBuilder();
                var outTask = ConsumeAsync(proc.StandardOutput, line => job.HandleLine(line, progress));
                var errTask = ConsumeAsync(proc.StandardError, line =>
                {
                    job.HandleLine(line, progress);
                    if (errTail.Length < 4000)
                        errTail.AppendLine(line);
                });

                try
                {
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    YtDlpRunner.KillTree(proc);
                }

                await Task.WhenAll(outTask, errTask);
                job.Proc = null;

                if (job.State is JobState.Paused or JobState.Cancelled)
                {
                    // user-driven state change wins
                }
                else if (proc.ExitCode == 0)
                {
                    completed = true;
                    job.OutputPath = FindOutputFile(job);
                }
                else
                {
                    error = Summarize(errTail.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            job.DetachRun();

            if (job.State == JobState.Paused)
            {
                // stays in the queue; slot is freed
            }
            else if (job.State == JobState.Cancelled)
            {
                ActiveJobs.Remove(job);
            }
            else if (completed)
            {
                job.SpeedText = "";
                job.EtaText = "";
                job.State = JobState.Completed;
                AddToHistory(job);
                JobCompleted?.Invoke(this, job);
            }
            else
            {
                job.State = JobState.Error;
                if (error is not null && YouTubeLoginHelper.IsLoginRequired(error))
                {
                    job.ErrorText = "YouTube sign-in required";
                    LoginRequiredDetected?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    job.ErrorText = error ?? "Download failed";
                }
            }

            TryStartNext();
        }
    }

    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".vtt", ".srt", ".ass", ".ssa", ".ttml", ".srvf", ".srv1", ".srv2", ".srv3", ".lrc", ".sami" };

    private static string? FindOutputFile(DownloadJob job)
    {
        try
        {
            var ext = job.Options.Mode == DownloadMode.Audio
                ? (job.Options.AudioFormat?.Split(' ')[0].ToLowerInvariant() ?? "m4a")
                : (job.Options.Container ?? "mp4").ToLowerInvariant();
            var expected = Path.Combine(job.FolderPath, $"{PathUtil.Sanitize(job.Item.Title)} [{job.Item.Id}].{ext}");
            if (File.Exists(expected))
                return expected;

            var actual = Directory.GetFiles(job.FolderPath, $"* [{job.Item.Id}].*")
                .FirstOrDefault(f =>
                    !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) &&
                    !SubtitleExtensions.Contains(Path.GetExtension(f)));
            return actual;
        }
        catch
        {
            return null;
        }
    }

    private void AddToHistory(DownloadJob job)
    {
        var item = job.Item;
        var opts = job.Options;

        var entry = new HistoryEntry
        {
            Id = item.Id,
            Title = item.Title,
            Url = item.Url,
            Channel = item.Channel,
            ThumbnailUrl = item.ThumbnailUrl,
            DurationSeconds = (int)(item.Duration?.TotalSeconds ?? 0),
            FilePath = job.OutputPath ?? Path.Combine(job.FolderPath, $"{item.Title} [{item.Id}].mp4"),
            SizeBytes = job.TotalBytes > 0
                ? (long)job.TotalBytes
                : (job.OutputPath is { } p && File.Exists(p) ? new FileInfo(p).Length : 0),
            ModeText = opts.Mode == DownloadMode.Audio ? "Audio" : "Video",
            QualityText = opts.Mode == DownloadMode.Audio ? opts.AudioFormat ?? "" : opts.Quality.Label,
            AudioFormat = opts.AudioFormat ?? "",
            IncludeSubtitles = opts.IncludeSubtitles,
            PlaylistFolder = string.IsNullOrWhiteSpace(opts.PlaylistFolder) ? "" : PathUtil.Sanitize(opts.PlaylistFolder),
            CompletedAt = DateTime.Now
        };

        History.Insert(0, entry);
        HistoryStore.Save(History);
    }

    public void PauseJob(DownloadJob job)
    {
        if (job.State != JobState.Downloading)
            return;
        if (job.Proc is { } p)
            YtDlpRunner.KillTree(p);
        job.State = JobState.Paused;
    }

    public void ResumeJob(DownloadJob job)
    {
        if (job.State != JobState.Paused)
            return;
        job.State = JobState.Queued;
        TryStartNext();
    }

    public void CancelJob(DownloadJob job)
    {
        if (job.State is JobState.Completed or JobState.Error)
            return;
        if (job.Cts is { } cts)
            cts.Cancel();
        if (job.Proc is { } p)
            YtDlpRunner.KillTree(p);
        job.State = JobState.Cancelled;
    }

    public void RetryJob(DownloadJob job)
    {
        if (job.State != JobState.Error)
            return;
        job.ErrorText = null;
        job.Percent = 0;
        job.SpeedText = "";
        job.EtaText = "";
        job.SizeText = "";
        job.TotalBytes = 0;
        job.IsIndeterminate = false;
        job.OutputPath = null;
        job.State = JobState.Queued;
        TryStartNext();
    }

    public void RemoveJob(DownloadJob job)
    {
        if (!job.IsDone)
            return;
        ActiveJobs.Remove(job);
    }

    public void PauseAll()
    {
        _pauseAll = true;
        foreach (var job in ActiveJobs.Where(j => j.State == JobState.Downloading).ToList())
            PauseJob(job);
    }

    public void ResumeAll()
    {
        _pauseAll = false;
        foreach (var job in ActiveJobs.Where(j => j.State == JobState.Paused).ToList())
            job.State = JobState.Queued;
        TryStartNext();
    }

    public void ClearFinished()
    {
        foreach (var job in ActiveJobs.Where(j => j.IsDone).ToList())
            ActiveJobs.Remove(job);
    }

    private static IEnumerable<string> BuildArgs(DownloadJob job)
    {
        var o = job.Options;

        yield return "--newline";
        yield return "--no-warnings";
        yield return "--ffmpeg-location";
        yield return EngineManager.BinDir;
        yield return "--socket-timeout";
        yield return "20";
        if (o.Connections > 1)
        {
            yield return "--concurrent-fragments";
            yield return o.Connections.ToString();
        }
        yield return "--progress-template";
        yield return "download:__DL__%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.speed)s|%(progress.eta)s|%(progress._percent_str)s";
        yield return "--progress-template";
        yield return "postprocess:__PP__%(postprocessor._status)s|%(postprocessor.name)s";

        yield return job.Item.Url;

        if (o.Mode == DownloadMode.Audio)
        {
            yield return "-f";
            yield return o.Quality.FormatArg;
            yield return "-x";
            yield return "--audio-format";
            yield return AudioFormatArg(o.AudioFormat);
            yield return "--audio-quality";
            yield return "0";
        }
        else
        {
            yield return "-f";
            yield return o.Quality.FormatArg;

            if (o.Container is { } container)
            {
                yield return "--merge-output-format";
                yield return container.ToLowerInvariant();
            }

            if (o.IncludeSubtitles)
            {
                yield return "--write-subs";
                yield return "--write-auto-subs";
                yield return "--sub-langs";
                yield return "en.*";
                yield return "--embed-subs";
            }
        }

        yield return "--output";
        yield return Path.Combine(job.FolderPath, "%(title)s [%(id)s].%(ext)s");
    }

    private static string AudioFormatArg(string? label) =>
        string.IsNullOrWhiteSpace(label) ? "m4a" : label.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();

    private static string Summarize(string output)
    {
        var lines = output
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Where(l => !l.StartsWith("[youtube] Extracting URL"))
            .TakeLast(6);

        var text = string.Join(" ", lines);
        return string.IsNullOrWhiteSpace(text) ? "Download failed — yt-dlp exited with an error." : text;
    }

    private static async Task ConsumeAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync() is { } line)
            onLine(line);
    }
}
