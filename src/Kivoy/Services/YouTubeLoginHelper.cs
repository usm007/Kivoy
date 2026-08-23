namespace Kivoy.Services;

public static class YouTubeLoginHelper
{
    private static readonly string[] Markers =
    {
        "sign in to confirm",
        "please sign in",
        "sign in if you",
        "login required",
        "log in required",
        "use --cookies",
        "cookies-from-browser",
        "age-restricted",
        "confirm your age",
        "inappropriate for some users",
        "age restricted",
        "members-only",
        "join this channel",
        "private video",
        "not a bot"
    };

    public static bool IsLoginRequired(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return false;

        foreach (var marker in Markers)
        {
            if (output.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
