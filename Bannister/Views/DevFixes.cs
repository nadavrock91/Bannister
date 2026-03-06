using Bannister.Services;
using Bannister.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bannister.Helpers
{
    public static class DevFixes
    {
        /// <summary>
        /// Run whatever fix is currently needed.
        /// Just update this method whenever you need to run a new fix.
        /// </summary>
        public static async Task RunCurrentFix(DatabaseService db, AuthService auth)
        {
            // FIX: Clear LastMeaningfulEscalation so escalation timer works properly
            // This is needed because the old code was auto-setting it to "now" every time
            // which kept resetting the timer to 30 days.
            // After this fix runs, users will need to manually click "Start" to begin tracking.
            
            try
            {
                var conn = await db.GetConnectionAsync();
                
                // Clear LastMeaningfulEscalation for all games
                await conn.ExecuteAsync("UPDATE games SET LastMeaningfulEscalation = NULL");
                
                System.Diagnostics.Debug.WriteLine("✓ DevFix: Cleared LastMeaningfulEscalation for all games");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ DevFix failed: {ex.Message}");
                throw; // Re-throw so the UI can show the error
            }
        }
    }
}
