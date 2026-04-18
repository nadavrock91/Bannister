using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bannister.Models;


namespace Bannister.Services
{
    public class DragonService
    {
        private readonly DatabaseService _db;

        public DragonService(DatabaseService db) => _db = db;

        /// <summary>
        /// Get the main/root dragon for a specific game (ParentDragonId = null)
        /// </summary>
        public async Task<Dragon?> GetDragonAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.Table<Dragon>()
                .Where(x => x.Username == username && x.Game == game && x.ParentDragonId == null)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> HasDragonAsync(string username, string game)
        {
            var dragon = await GetDragonAsync(username, game);
            return dragon != null;
        }

        /// <summary>
        /// Create or update the main/root dragon for a game
        /// </summary>
        public async Task<Dragon> CreateOrUpdateDragonAsync(string username, string game, string title, string description, string imagePath)
        {
            var conn = await _db.GetConnectionAsync();
            var existing = await GetDragonAsync(username, game);

            if (existing != null)
            {
                existing.Title = title;
                existing.Description = description;
                existing.ImagePath = imagePath;
                await conn.UpdateAsync(existing);
                return existing;
            }
            else
            {
                var dragon = new Dragon
                {
                    Username = username,
                    Game = game,
                    ParentDragonId = null,
                    Title = title,
                    Description = description,
                    ImagePath = imagePath,
                    CreatedAt = DateTime.UtcNow
                };
                await conn.InsertAsync(dragon);
                return dragon;
            }
        }

        // NEW METHODS FOR HIERARCHY
        
        /// <summary>
        /// Get all dragons for a game (including hierarchy - both main and sub-dragons)
        /// </summary>
        public async Task<List<Dragon>> GetDragonsAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.Table<Dragon>()
                .Where(x => x.Username == username && x.Game == game)
                .ToListAsync();
        }

        /// <summary>
        /// Create a new dragon (can be root or child)
        /// </summary>
        public async Task<Dragon> CreateDragonAsync(Dragon dragon)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.InsertAsync(dragon);
            return dragon;
        }

        /// <summary>
        /// Update an existing dragon
        /// </summary>
        public async Task UpdateDragonAsync(Dragon dragon)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.UpdateAsync(dragon);
        }

        /// <summary>
        /// Delete a dragon by ID
        /// </summary>
        public async Task DeleteDragonAsync(int dragonId)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.DeleteAsync<Dragon>(dragonId);
        }

        /// <summary>
        /// Get all ACTIVE dragons for a user (ALL dragons, including sub-dragons, not slain and not irrelevant)
        /// This is used for the Active Dragons page on the home screen
        /// </summary>
        public async Task<List<Dragon>> GetActiveDragonsAsync(string username)
        {
            var conn = await _db.GetConnectionAsync();
            var dragons = await conn.Table<Dragon>()
                .Where(x => x.Username == username)
                .ToListAsync();
            
            // Return ALL active dragons (both main and sub-dragons) where SlainAt is null AND not irrelevant
            return dragons
                .Where(d => d.SlainAt == null && !d.IsIrrelevant)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Get all SLAIN dragons for a user (ALL dragons, including sub-dragons, that are slain)
        /// </summary>
        public async Task<List<Dragon>> GetSlainDragonsAsync(string username)
        {
            var conn = await _db.GetConnectionAsync();
            var dragons = await conn.Table<Dragon>()
                .Where(x => x.Username == username)
                .ToListAsync();
            
            // Return ALL slain dragons (both main and sub-dragons) where SlainAt is not null
            return dragons
                .Where(d => d.SlainAt != null)
                .OrderByDescending(d => d.SlainAt)
                .ToList();
        }

        /// <summary>
        /// Get all IRRELEVANT dragons for a user (dragons marked as no longer pursuing)
        /// </summary>
        public async Task<List<Dragon>> GetIrrelevantDragonsAsync(string username)
        {
            var conn = await _db.GetConnectionAsync();
            var dragons = await conn.Table<Dragon>()
                .Where(x => x.Username == username)
                .ToListAsync();
            
            // Return ALL irrelevant dragons where IsIrrelevant is true (and not slain)
            return dragons
                .Where(d => d.IsIrrelevant && d.SlainAt == null)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Mark a dragon as irrelevant (no longer pursuing)
        /// </summary>
        public async Task MarkDragonIrrelevantAsync(int dragonId)
        {
            var conn = await _db.GetConnectionAsync();
            var dragon = await conn.GetAsync<Dragon>(dragonId);
            if (dragon != null)
            {
                dragon.IsIrrelevant = true;
                await conn.UpdateAsync(dragon);
            }
        }

        /// <summary>
        /// Restore a dragon from irrelevant back to active
        /// </summary>
        public async Task RestoreDragonAsync(int dragonId)
        {
            var conn = await _db.GetConnectionAsync();
            var dragon = await conn.GetAsync<Dragon>(dragonId);
            if (dragon != null)
            {
                dragon.IsIrrelevant = false;
                await conn.UpdateAsync(dragon);
            }
        }

        /// <summary>
        /// Mark a dragon as slain (set SlainAt timestamp)
        /// </summary>
        public async Task SlayDragonAsync(string username, string game)
        {
            var dragon = await GetDragonAsync(username, game);
            if (dragon != null && dragon.SlainAt == null)
            {
                var conn = await _db.GetConnectionAsync();
                dragon.SlainAt = DateTime.UtcNow;
                await conn.UpdateAsync(dragon);
            }
        }

        /// <summary>
        /// Check if the main dragon for a game is slain
        /// </summary>
        public async Task<bool> IsDragonSlainAsync(string username, string game)
        {
            var dragon = await GetDragonAsync(username, game);
            return dragon?.SlainAt != null;
        }

        /// <summary>
        /// Format a duration into human-readable string (e.g., "2y 3mo", "5mo 12d", "3d")
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 365)
            {
                int years = (int)(duration.TotalDays / 365);
                int months = (int)((duration.TotalDays % 365) / 30);
                return months > 0 ? $"{years}y {months}mo" : $"{years}y";
            }
            else if (duration.TotalDays >= 30)
            {
                int months = (int)(duration.TotalDays / 30);
                int days = (int)(duration.TotalDays % 30);
                return days > 0 ? $"{months}mo {days}d" : $"{months}mo";
            }
            else if (duration.TotalDays >= 1)
            {
                return $"{(int)duration.TotalDays}d";
            }
            else if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h";
            }
            else
            {
                return $"{(int)duration.TotalMinutes}m";
            }
        }
    }
}
