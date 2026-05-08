using SQLite;

namespace Bannister.Models;

/// <summary>
/// An audio library item — quote, anecdote, lesson, or insight.
/// Can have an audio file (browsed or generated from text).
/// </summary>
[Table("audio_items")]
public class AudioItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>
    /// The text content (quote, anecdote, lesson)
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Category for organizing (philosophy, business, motivation, etc.)
    /// </summary>
    [Indexed]
    public string Category { get; set; } = "General";

    /// <summary>
    /// Optional source — who said it, which book/video
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Optional personal notes or interpretation
    /// </summary>
    public string Notes { get; set; } = "";

    /// <summary>
    /// Path to audio file (relative to app data folder).
    /// Can be a browsed file or generated from text.
    /// </summary>
    public string AudioPath { get; set; } = "";

    /// <summary>
    /// How the audio was created: "browsed", "generated", or "" if no audio
    /// </summary>
    public string AudioSource { get; set; } = "";

    /// <summary>
    /// Duration of audio in seconds (0 if unknown or no audio)
    /// </summary>
    public int AudioDurationSeconds { get; set; } = 0;

    /// <summary>
    /// How many times this item has been played/shown in rotation
    /// </summary>
    public int TimesPlayed { get; set; } = 0;

    /// <summary>
    /// When this item was last played
    /// </summary>
    public DateTime? LastPlayedAt { get; set; }

    /// <summary>
    /// Whether this is a favorite (plays more often in rotation)
    /// </summary>
    public bool IsFavorite { get; set; } = false;

    /// <summary>
    /// When this item was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When this item was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    // Computed properties

    [Ignore]
    public bool HasAudio => !string.IsNullOrEmpty(AudioPath);

    [Ignore]
    public string AudioStatusDisplay => HasAudio
        ? (AudioSource == "generated" ? "🔊 Generated" : "🔊 File")
        : "🔇 No audio";

    [Ignore]
    public string TextPreview => Text.Length > 80 ? Text.Substring(0, 77) + "..." : Text;

    [Ignore]
    public string TimesPlayedDisplay => TimesPlayed == 0 ? "Never played" : $"Played {TimesPlayed}x";
}
