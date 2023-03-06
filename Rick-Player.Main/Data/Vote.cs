namespace Rick_Player.Main.Data;

public class Vote
{
    public Vote(Track votedTrack, Client client)
    {
        VotedTrack = votedTrack;
        Client = client;
    }

    public Track VotedTrack { get; set; }
    public Client Client { get; set; }
    public bool IsOnYoutubeQueue { get; set; }
}