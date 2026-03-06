using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bannister.Models;


namespace Bannister.Services
{
    public class GameService
    {
        private readonly DatabaseService _db;

        public GameService(DatabaseService db) => _db = db;

        public async Task<List<Game>> GetGamesAsync(string username)
        {
            var conn = await _db.GetConnectionAsync();
            var games = await conn.Table<Game>()
                .Where(x => x.Username == username && x.IsActive)
                .ToListAsync();
            
            // Sort alphabetically (case-insensitive) in memory
            return games.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<Game?> GetGameAsync(string username, string gameId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.Table<Game>()
                .Where(x => x.Username == username && x.GameId == gameId)
                .FirstOrDefaultAsync();
        }

        public async Task<Game> CreateGameAsync(string username, string displayName)
        {
            var conn = await _db.GetConnectionAsync();
            string gameId = displayName.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

            var games = await GetGamesAsync(username);
            int sortOrder = games.Count > 0 ? games.Max(g => g.SortOrder) + 1 : 0;

            var game = new Game
            {
                Username = username,
                GameId = gameId,
                DisplayName = displayName,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await conn.InsertAsync(game);
            return game;
        }

        public async Task DeleteGameAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            var game = await conn.GetAsync<Game>(id);
            if (game != null)
            {
                game.IsActive = false;
                await conn.UpdateAsync(game);
            }
        }

        /// <summary>
        /// Reset meaningful escalation timer to 30 days
        /// </summary>
        public async Task ResetMeaningfulEscalationAsync(string username, string gameId)
        {
            var game = await GetGameAsync(username, gameId);
            if (game != null)
            {
                game.LastMeaningfulEscalation = DateTime.UtcNow;
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(game);
            }
        }

        /// <summary>
        /// Check if meaningful escalation timer has expired (0 days remaining)
        /// </summary>
        public async Task<bool> HasEscalationExpiredAsync(string username, string gameId)
        {
            var game = await GetGameAsync(username, gameId);
            return game != null && game.DaysRemaining <= 0;
        }

        /// <summary>
        /// Initialize meaningful escalation timer for a new game (set to now = 30 days remaining)
        /// </summary>
        public async Task InitializeMeaningfulEscalationAsync(string username, string gameId)
        {
            var game = await GetGameAsync(username, gameId);
            if (game != null && !game.LastMeaningfulEscalation.HasValue)
            {
                game.LastMeaningfulEscalation = DateTime.UtcNow;
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(game);
            }
        }

        /// <summary>
        /// Toggle the escalation timer enabled/disabled state for a game
        /// </summary>
        public async Task ToggleEscalationTimerAsync(string username, string gameId)
        {
            var game = await GetGameAsync(username, gameId);
            if (game != null)
            {
                game.IsEscalationTimerDisabled = !game.IsEscalationTimerDisabled;
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(game);
            }
        }
    }
}