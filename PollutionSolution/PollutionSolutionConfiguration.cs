using System;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace PollutionSolution {
    /// <summary>
    /// Configuration data model for the Pollution Solution mod, serialized to XML in the game directory.
    /// </summary>
    [ConfigurationPath("PollutionSolution.xml")]
    public class PollutionSolutionConfiguration {
        /// <summary>
        /// Gets or sets whether noise pollution removal is enabled.
        /// </summary>
        public bool RemoveNoisePollution { get; set; } = true;

        /// <summary>
        /// Gets or sets whether ground pollution removal is enabled.
        /// </summary>
        public bool RemoveGroundPollution { get; set; } = true;

        /// <summary>
        /// Gets or sets whether water pollution removal is enabled.
        /// </summary>
        public bool RemoveWaterPollution { get; set; } = true;

        /// <summary>
        /// Target percentage for noise pollution cleanup, ranging from 0.0 (0%) to 1.0 (100%).
        /// </summary>
        public float NoisePollutionCleanupPercentage { get; set; } = 1.0f;

        /// <summary>
        /// Target percentage for ground pollution cleanup, ranging from 0.0 (0%) to 1.0 (100%).
        /// </summary>
        public float GroundPollutionCleanupPercentage { get; set; } = 1.0f;

        /// <summary>
        /// Target percentage for water pollution cleanup, ranging from 0.0 (0%) to 1.0 (100%).
        /// </summary>
        public float WaterPollutionCleanupPercentage { get; set; } = 1.0f;
    }
}
