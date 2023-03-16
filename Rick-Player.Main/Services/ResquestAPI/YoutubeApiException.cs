namespace Rick_Player.Main.Services.ResquestAPI;

public class YoutubeApiException : Exception
{
    public YoutubeApiException()
    {
    }

    public YoutubeApiException(string message) : base(message)
    {
    }

    public YoutubeApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}