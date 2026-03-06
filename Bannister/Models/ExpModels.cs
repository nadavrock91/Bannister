using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bannister.Models
{
    [Table("exp_state")]
    public class ExpState
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Indexed]
        public string Username { get; set; } = "";
        
        [Indexed]
        public string Game { get; set; } = "";
        
        public int TotalExp { get; set; } = 0;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    
    [Table("exp_log")]
    public class ExpLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Indexed]
        public string Username { get; set; } = "";
        
        [Indexed]
        public string Game { get; set; } = "";
        
        // ← NEW: Track which activity was completed
        [Indexed]
        public int ActivityId { get; set; } = 0;
        
        public string ActivityName { get; set; } = "";
        
        public int DeltaExp { get; set; } = 0;
        
        public int TotalExp { get; set; } = 0;
        
        // ← NEW: Level before activity completion
        public int LevelBefore { get; set; } = 0;
        
        // ← NEW: Level after activity completion
        public int LevelAfter { get; set; } = 0;
        
        [Indexed]
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    }
}
