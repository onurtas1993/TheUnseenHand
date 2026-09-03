using TheUnseenHand.Models;

namespace TheUnseenHand.Services;

public interface IMacroJsonService
{
    Task SaveAsync(string filePath, AppSettings settings);

    Task<AppSettings> LoadAsync(string filePath);
}