using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bannister.Models
{
    [Table("game_dragon")]
    public class Dragon
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Username { get; set; } = "";

        public string Game { get; set; } = "";

        /// <summary>
        /// Parent dragon ID for hierarchical structure (null for root dragons)
        /// </summary>
        [Indexed]
        public int? ParentDragonId { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string ImagePath { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SlainAt { get; set; }

        /// <summary>
        /// If true, automatically increment days battling each day
        /// </summary>
        public bool IsAutoIncrement { get; set; } = false;

        /// <summary>
        /// Last date when auto-increment was applied (to prevent double counting)
        /// </summary>
        public DateTime? LastAutoIncrementDate { get; set; }

        /// <summary>
        /// If true, this dragon is marked as irrelevant (no longer pursuing)
        /// </summary>
        public bool IsIrrelevant { get; set; } = false;
    }
}