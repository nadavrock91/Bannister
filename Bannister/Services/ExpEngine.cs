using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bannister.Services
{
    public static class ExpEngine
    {
        public const int MaxLevel = 100;
        public const double A = 10.0;
        public const double Power = 2.0;

        // Precomputed thresholds for levels 0-100
        private static readonly int[] LevelThresholds = ComputeThresholds();

        private static int[] ComputeThresholds()
        {
            var thresholds = new int[MaxLevel + 1];
            thresholds[0] = 0;
            thresholds[1] = 0;

            double total = 0;
            for (int i = 2; i <= MaxLevel; i++)
            {
                total += A * Math.Pow(i - 1, Power);
                thresholds[i] = (int)Math.Round(total);
            }
            return thresholds;
        }

        /// <summary>
        /// Total EXP needed to be at the START of a level
        /// </summary>
        public static int TotalExpAtLevelStart(int level)
        {
            level = Math.Clamp(level, 1, MaxLevel);
            return LevelThresholds[level];
        }

        /// <summary>
        /// Alias for TotalExpAtLevelStart - cumulative EXP required to reach a level
        /// </summary>
        public static int CumulativeExpForLevel(int level)
        {
            return TotalExpAtLevelStart(level);
        }

        /// <summary>
        /// EXP span to go from level -> level+1
        /// </summary>
        public static int ExpSpanForLevel(int level)
        {
            level = Math.Clamp(level, 1, MaxLevel);
            return (int)Math.Round(A * Math.Pow(level, Power));
        }

        /// <summary>
        /// Get level from total EXP
        /// </summary>
        public static int GetLevelFromTotalExp(int totalExp)
        {
            if (totalExp < 0) totalExp = 0;

            int level = 1;
            while (level < MaxLevel)
            {
                int nextStart = TotalExpAtLevelStart(level + 1);
                if (totalExp < nextStart) break;
                level++;
            }
            return level;
        }

        /// <summary>
        /// Get full progress info: level, EXP into current level, EXP needed for this level
        /// </summary>
        public static (int level, int expIntoLevel, int expNeededThisLevel) GetProgress(int totalExp)
        {
            int level = GetLevelFromTotalExp(totalExp);
            int floor = TotalExpAtLevelStart(level);
            int ceil = (level < MaxLevel) ? TotalExpAtLevelStart(level + 1) : floor + ExpSpanForLevel(level);
            int span = Math.Max(1, ceil - floor);
            int into = Math.Clamp(totalExp - floor, 0, span);
            return (level, into, span);
        }

        /// <summary>
        /// Calculate EXP for a rule with cutoff level (1% of level span)
        /// </summary>
        public static int ExpForRuleWithCutoff(int cutoffLevel)
        {
            cutoffLevel = Math.Clamp(cutoffLevel, 1, MaxLevel);
            int span = ExpSpanForLevel(cutoffLevel);
            int exp = (int)Math.Round(span / 100.0);
            return Math.Max(1, exp);
        }

        /// <summary>
        /// Check if an activity is still meaningful at current level
        /// </summary>
        public static bool IsActivityMeaningful(int currentLevel, int meaningfulUntilLevel)
        {
            return currentLevel <= meaningfulUntilLevel;
        }

        /// <summary>
        /// Calculate EXP as a percentage of the current level's span
        /// If cutoffLevel is specified and currentLevel exceeds it, uses cutoffLevel's span instead
        /// </summary>
        public static int ExpForPercentOfLevel(int currentLevel, double percent, int cutoffLevel = 100)
        {
            currentLevel = Math.Clamp(currentLevel, 1, MaxLevel);
            cutoffLevel = Math.Clamp(cutoffLevel, 1, MaxLevel);
            
            // Use the lower of current level or cutoff level for calculation
            int levelForCalc = Math.Min(currentLevel, cutoffLevel);
            
            int span = ExpSpanForLevel(levelForCalc);
            int exp = (int)Math.Round(span * percent / 100.0);
            return Math.Max(1, exp);
        }
    }
}
