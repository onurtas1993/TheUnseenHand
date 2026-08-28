namespace TheUnseenHand.Services;

public interface IMobRecognitionService
{
    Task EnsureAvailableAsync(CancellationToken cancellationToken);

    Task<string> RecognizeCurrentAsync(CancellationToken cancellationToken);
}
