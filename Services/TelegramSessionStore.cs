using System.Collections.Concurrent;

namespace WebApplication1.Services
{
    /// <summary>
    /// Thread-safe in-memory store for active Telegram Bot user sessions.
    /// Registered as a Singleton to persist conversation state across individual
    /// HTTP webhook requests.
    /// </summary>
    public class TelegramSessionStore : ITelegramSessionStore
    {
        private readonly ConcurrentDictionary<string, BotSession> _sessions = new();
        private readonly ILogger<TelegramSessionStore> _logger;

        public TelegramSessionStore(ILogger<TelegramSessionStore> logger)
        {
            _logger = logger;
        }

        public BotSession GetOrCreate(string chatId)
        {
            return _sessions.GetOrAdd(chatId, id => new BotSession
            {
                ChatId = id,
                Step = BotStep.Idle,
                Lang = "ar",
                LastActiveUtc = DateTime.UtcNow
            });
        }

        public void Update(BotSession session)
        {
            session.LastActiveUtc = DateTime.UtcNow;
            _sessions.AddOrUpdate(session.ChatId, session, (_, _) => session);
        }

        public void Clear(string chatId)
        {
            if (_sessions.TryRemove(chatId, out _))
            {
                _logger.LogInformation("[TelegramSessionStore] Session cleared for ChatId {ChatId}.", chatId);
            }
        }
    }
}
