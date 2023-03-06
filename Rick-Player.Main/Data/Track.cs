namespace Rick_Player.Main.Data;

public class Track
{
    public enum CoverSize
    {
        Large,
        Medium,
        Small
    }

    public List<string?> CoverSizesUrl { get; set; } = new();
    public List<string?> ArtistNames { get; set; } = new();

    public string? VideoId { get; set; }
    public string? Name { get; set; }
    public string? AlbumName { get; set; }
    public int DurationMs { get; set; }
    public int? ProgressMs  { get; set; }

    public Track MakeThisDummy()
    {
        VideoId = "0";
        Name = "Nothing being played";
        AlbumName = "No album";
        DurationMs = 0;
        ArtistNames = new() { "No artist" };
        CoverSizesUrl = new() { "", "/img/no_cover.jpg", "" };

        return this;
    }
}