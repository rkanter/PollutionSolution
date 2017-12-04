using System;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace PollutionSolution
{
    [ConfigurationPath("PollutionSolution.xml")]
    public class PollutionSolutionConfiguration
    {
        public bool RemoveNoisePollution { get; set; } = true;
        public bool RemoveGroundPollution { get; set; } = true;
        public bool RemoveWaterPollution { get; set; } = true;
    }
}
