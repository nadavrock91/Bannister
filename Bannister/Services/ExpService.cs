using Bannister.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bannister.Services
{
    public class ExpService
    {
        private readonly DatabaseService _db;

        public ExpService(DatabaseService db) => _db = db;

        public async Task EnsureUserStateAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var existing = await conn.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == game)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                // Calculate total EXP from historical exp_log entries
                var logs = await conn.Table<ExpLog>()
                    .Where(x => x.Username == username && x.Game == game)
                    .ToListAsync();
                
                int totalExp = logs.Sum(log => log.DeltaExp);
                
                System.Diagnostics.Debug.WriteLine($"EnsureUserStateAsync: Creating new state for {username}/{game}");
                System.Diagnostics.Debug.WriteLine($"  Found {logs.Count} historical exp_log entries");
                System.Diagnostics.Debug.WriteLine($"  Calculated TotalExp from history: {totalExp}");

                await conn.InsertAsync(new ExpState
                {
                    Username = username,
                    Game = game,
                    TotalExp = totalExp, // Use calculated total from history
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        public async Task<int> GetTotalExpAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var state = await conn.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == game)
                .FirstOrDefaultAsync();
            return state?.TotalExp ?? 0;
        }

        public async Task<(int level, int expIntoLevel, int expNeeded)> GetProgressAsync(string username, string game)
        {
            int totalExp = await GetTotalExpAsync(username, game);
            return ExpEngine.GetProgress(totalExp);
        }

        public async Task<int> ApplyExpAsync(string username, string game, string activityName, int deltaExp, int activityId = 0)
        {
            var conn = await _db.GetConnectionAsync();

            // Get current state
            var state = await conn.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == game)
                .FirstOrDefaultAsync();

            int currentExp = state?.TotalExp ?? 0;
            int newTotal = currentExp + deltaExp; // Allow negative EXP for penalty games

            // Calculate levels before and after
            var (levelBefore, _, _) = ExpEngine.GetProgress(currentExp);
            var (levelAfter, _, _) = ExpEngine.GetProgress(newTotal);

            // Update or insert state
            if (state != null)
            {
                state.TotalExp = newTotal;
                state.UpdatedAt = DateTime.UtcNow;
                await conn.UpdateAsync(state);
            }
            else
            {
                await conn.InsertAsync(new ExpState
                {
                    Username = username,
                    Game = game,
                    TotalExp = newTotal,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Log the change with level tracking
            await conn.InsertAsync(new ExpLog
            {
                Username = username,
                Game = game,
                ActivityId = activityId,
                ActivityName = activityName,
                DeltaExp = deltaExp,
                TotalExp = newTotal,
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                LoggedAt = DateTime.UtcNow
            });

            return newTotal;
        }

        public async Task<List<ExpLog>> GetRecentLogsAsync(string username, string game, int limit = 20)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.Table<ExpLog>()
                .Where(x => x.Username == username && x.Game == game)
                .OrderByDescending(x => x.Id)
                .Take(limit)
                .ToListAsync();
        }
    }
}