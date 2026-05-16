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

            
            try
            {
                // ===== FORCE MASTER MODE =====
                // Diagnostic for broken login: clear the device_mode preference
                // and any stale sync credentials so this device behaves like a
                // fresh master install. Does NOT touch the database file or
                // the users table.

                string before = Preferences.Default.Get("device_mode", "(unset)");
                System.Diagnostics.Debug.WriteLine($"[DevFix] device_mode before: {before}");

                Preferences.Default.Set("device_mode", "Master");

                string after = Preferences.Default.Get("device_mode", "(unset)");
                System.Diagnostics.Debug.WriteLine($"[DevFix] device_mode after:  {after}");

                // Clear last sync timestamp too (cosmetic)
                Preferences.Default.Set("sync_last_utc", 0L);

                System.Diagnostics.Debug.WriteLine("[DevFix] ✓ Forced device mode to Master");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ DevFix failed: {ex.Message}");
                throw; // Re-throw so the UI can show the error
            }
        }
    }
}
