namespace TubeDrop.Models;

public enum DownloadMode
{
    Video,
    Audio
}

public enum JobState
{
    Queued,
    Downloading,
    Paused,
    Processing,
    Completed,
    Error,
    Cancelled
}
