using ColossalFramework;
using ICities;
using System;
using System.Diagnostics;

namespace PollutionSolution
{
    // TODO: better overall organization - i'm not sure how others did this, it only seems to work correctly if it's all in the one class...
    // TODO: useful logging
    // TODO: ansel testing
    // TODO: license
    // TODO: README
    public class PollutionSolution : IUserMod
    {
        // It seems that if you make this too negative, it's less effective
        // (there's probably some underflow somewhere in that case)
        private const int noiseAmount = Int32.MinValue / 5;

        // This didn't have the same problem as the noise, but it's safer to stay a bit away from the max int
        private const int pollutionAmount = Int32.MaxValue / 5;

        // TODO: is there a better way to do this available to both classes than by making it static?
        private static bool removeNoisePollution = true;
        private static bool removeGroundPollution = true;
        private static bool removeWaterPollution = true;

        public string Name
        {
            get { return "Pollution Solution"; }
        }

        public string Description
        {
            get { return "Enable/disable Noise, Ground, and Water pollution (by ShadowLink721)"; }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            // TODO: need to save/load from file
            UIHelperBase settingsGroup = helper.AddGroup("Remove");
            settingsGroup.AddCheckbox("Noise Pollution", true, (selected) => { removeNoisePollution = selected; });
            settingsGroup.AddCheckbox("Ground Pollution", true, (selected) => { removeGroundPollution = selected; });
            settingsGroup.AddCheckbox("Water Pollution", true, (selected) => { removeWaterPollution = selected; });
        }

        public class PollutionLogic : ThreadingExtensionBase
        {
            private readonly Stopwatch timer = new Stopwatch();

            public override void OnCreated(IThreading threading)
            {
                base.OnCreated(threading);
                timer.Start();
            }

            public override void OnReleased()
            {
                timer.Reset();
                base.OnReleased();
            }
            public override void OnAfterSimulationFrame()
            {
                // Probably not in a normal game type if the district manager doesn't actually exist.
                if (Singleton<DistrictManager>.exists &&
                    // No need to do anything if we're paused
                    !SimulationManager.instance.SimulationPaused &&
                    // Don't do things faster than once per second
                    timer.ElapsedMilliseconds > 1000)
                {
                    if (removeNoisePollution)
                    {
                        ImmaterialResourceManager.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, noiseAmount);
                    }
                    if (removeGroundPollution)
                    {
                        Singleton<NaturalResourceManager>.instance.AddPollutionDisposeRate(pollutionAmount);
                    }
                    if (removeWaterPollution)
                    {
                        Singleton<TerrainManager>.instance.WaterSimulation.AddPollutionDisposeRate(pollutionAmount);
                    }

                    timer.Reset();
                    timer.Start();
                }

                base.OnAfterSimulationTick();
            }
        }
    }
}
