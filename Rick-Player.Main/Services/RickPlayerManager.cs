using System.Net;
using Rick_Player.Main.Data;
using Rick_Player.Main.Services.ResquestAPI;

namespace Rick_Player.Main.Services;

public class RickPlayerManager
{
    #region Attributes and Constructor
    public EventHandler? TimerUpdateEvent;
    public EventHandler? VotingQueueUpdateEvent;

    private readonly YoutubeManager _youtubeManager;
    private readonly TimerManager _timerManager;

    public Vote CurrentlyPlayingVote { get; private set; }
    public List<Vote> PreviouslyPlayedVotes { get; private set; }
    public List<Queue<Vote>> Votes { get; private set; }
    public List<Client> Clients { get; private set; }

    private YoutubeManager.Tokens _tokens;
    private int _loopPeriodInMs = 0;

    public RickPlayerManager(TimerManager timerManager, IWebHostEnvironment hostEnvironment)
    {
        _timerManager = timerManager;

        _youtubeManager = new YoutubeManager(hostEnvironment.IsProduction() ? "https://sparkflyblazor.azurewebsites.net/validate" : "https://localhost:5001/validate");

        CurrentlyPlayingVote = MakeDummyVote();
        PreviouslyPlayedVotes = new List<Vote>();
        Votes = new List<Queue<Vote>>();
        Clients = new List<Client>();
    }
    #endregion

    #region Youtube Methods
    public Uri YoutubeSignInUri(string state)
    {
        try
        {
            return _youtubeManager.RequestUserAuthorizationUri(state);
        }
        catch (Exception)
        {
            throw;
        }
    }
    public async Task YoutubeRequestTokensAsync(string authCode, string originalStateCode, string returnedStateCode)
    {
        try
        {
            _tokens = await _youtubeManager.RequestAccessAndRefreshTokensAsync(authCode, originalStateCode, returnedStateCode);

            _timerManager.Stop();
            Votes.Clear();
            Clients.Clear();
        }
        catch (Exception)
        {
            throw;
        }
    }
    private async Task YoutubeRefreshAccessTokenAsync()
    {
        try
        {
            _tokens.AccessToken = await _youtubeManager.RefreshAccessTokenAsync(_tokens.RefreshToken);
        }
        catch (Exception)
        {
            throw new YoutubeApiException("algo deu errado.");
        }
    }
    private async Task<Track> YoutubeGetCurrentlyPlayingAsync()
    {
        try
        {
            return await _youtubeManager.GetCurrentlyPlayingAsync() ?? new Track().MakeThisDummy();
        }
        catch (HttpRequestException ex)
        {
            try
            {
                await HandleHttpExceptionAsync(ex);

                return await _youtubeManager.GetCurrentlyPlayingAsync() ?? new Track().MakeThisDummy();
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
    public async Task<List<Track>> YoutubeSearchTracksAsync(string searchFor)
    {
        try
        {
            return await _youtubeManager.SearchTracksAsync(searchFor);
        }
        catch (HttpRequestException ex)
        {
            try
            {
                await HandleHttpExceptionAsync(ex);

                return await _youtubeManager.SearchTracksAsync(searchFor);
            }
            catch (Exception)
            {
                throw new YoutubeApiException("algo deu errado.");
            }
        }
    }
    private async Task YoutubeAddToPlaybackQueueAsync(Track track)
    {
        try
        {
            await _youtubeManager.AddToPlaybackQueueAsync(track);
        }
        catch (HttpRequestException ex)
        {
            try
            {
                await HandleHttpExceptionAsync(ex);

                await _youtubeManager.AddToPlaybackQueueAsync(track);
            }
            catch (Exception)
            {
                throw new YoutubeApiException("algo deu errado.");
            }
        }
    }
    #endregion

    #region Voting Queue Methods
    protected virtual void OnVotingQueueUpdate() => VotingQueueUpdateEvent?.Invoke(this, EventArgs.Empty);
    private Vote MakeDummyVote() => new(new Track().MakeThisDummy(), new Client("0", "Youtube"));
    private void ResetPriority(int priority)
    {
        Votes.RemoveAt(priority);

        if (priority == 0)
            PreviouslyPlayedVotes.Clear();
    }

    public Vote? TryPeekVotingQueue()
    {
        if (!Votes.Any())
            return null;

        return Votes[0].TryPeek(out Vote? voteOnTop) ? voteOnTop : null;
    }

    public void EnqueueVote(Track votedTrack, Client client)
    {
        if (votedTrack == null) throw new ArgumentNullException(nameof(votedTrack));
        if (client == null) throw new ArgumentNullException(nameof(client));
        int priority = 0;   // lower number means higher priority

        for (priority = 0; priority < Votes.Count; priority++)
        {
            if (Votes[priority].Any(v => v.Client.Id == client.Id))
                continue;

            break;
        }

        if (priority == 0 && PreviouslyPlayedVotes.Any(v => v.Client.Id == client.Id))
            priority = 1;

        if (priority >= Votes.Count)
            Votes.Add(new Queue<Vote>());

        Votes[priority].Enqueue(new Vote(votedTrack, client));

        OnVotingQueueUpdate();
    }

    public Vote? TryDequeueVote()
    {
        if (!Votes.Any())
            return null;

        Votes[0].TryDequeue(out Vote? dequeuedVote);

        if (!Votes[0].Any())
            ResetPriority(0);

        return dequeuedVote;
    }

    public void RemoveVote(Track track, Client client)
    {
        for (int i = 0; i < Votes.Count; i++)
        {
            if (Votes[i].Any(v => v.VotedTrack.VideoId == track.VideoId && v.Client.Id == client.Id) == false)
                continue;

            Votes[i] = new Queue<Vote>(Votes[i].Where(v => !(v.VotedTrack.VideoId == track.VideoId && v.Client.Id == client.Id)));

            if (!Votes[i].Any())
                ResetPriority(i);

            break;
        }

        OnVotingQueueUpdate();
    }

    public bool IsTrackVoted(Track track) => Votes.Exists(queue => queue.Where(vote => vote.VotedTrack.VideoId == track.VideoId).Any());
    #endregion

    #region Timer Methods
    protected virtual void OnTimerUpdate() => TimerUpdateEvent?.Invoke(this, EventArgs.Empty);
    public void StartTimer(int seconds = 100)
    {
        if (_timerManager.HasStarted)
            StopTimer();

        _timerManager.TimeElapsed += OnTimerElapsedAsync;

        _timerManager.Start(seconds);

        _loopPeriodInMs = seconds * 1000;
    }

    public void StopTimer()
    {
        _timerManager.TimeElapsed -= OnTimerElapsedAsync;

        _timerManager.Stop();
    }

    private async void OnTimerElapsedAsync(object source, EventArgs args)
    {
        Track newestTrack = await YoutubeGetCurrentlyPlayingAsync();
        Vote? nextVote = TryPeekVotingQueue();

        if (newestTrack.VideoId != CurrentlyPlayingVote.VotedTrack.VideoId)
        {
            if (nextVote is not null && newestTrack.VideoId == nextVote.VotedTrack.VideoId)
            {
                TryDequeueVote();

                PreviouslyPlayedVotes.Add(CurrentlyPlayingVote);
                CurrentlyPlayingVote = nextVote;
            }
            else
                CurrentlyPlayingVote = new Vote(newestTrack, new Client("0", "Youtube"));
        }

        if (nextVote is not null && !nextVote.IsOnYoutubeQueue && (newestTrack.DurationMs - newestTrack.ProgressMs) < _loopPeriodInMs * 2)
        {
            await YoutubeAddToPlaybackQueueAsync(nextVote.VotedTrack);
            nextVote.IsOnYoutubeQueue = true;
        }

        // TODO: else add a recommended track

        OnTimerUpdate();
    }
    #endregion

    #region Client Methods
    public void UpdateClient(Client clientUpdated) => Clients[Clients.FindIndex(c => c.Id == clientUpdated.Id)] = clientUpdated;    // TODO: change the client in the queue
    #endregion

    #region Other Methods
    private async Task HandleHttpExceptionAsync(HttpRequestException exception)
    {
        try
        {
            if (exception.StatusCode == HttpStatusCode.Unauthorized)
                await YoutubeRefreshAccessTokenAsync();
        }
        catch (Exception)
        {
             throw new YoutubeApiException("algo deu errado.");
        }
    }
    #endregion
}