using ColossalFramework;
using ICities;
using System;

namespace PollutionSolution
{
    public class PollutionSolution : IUserMod
    {
        // It seems that if you make this too negative (i.e. Int32), it's less effective
        // (there's probably some underflow somewhere in that case)
        // Let's just use Int16.MinValue as a relatively safe number
        private const int noiseAmount = Int16.MinValue;

        // This didn't have the same problem as the noise, but it's safer to stay away from the max Int32
        private const int pollutionAmount = Int16.MaxValue;

        // How frequently to remove the pollution, in frames
        // The game will process a different number of frames per wall time depending on the game speed and the ability of the computer
        // At 1x speed, there are 60 frames per second, so 60 here should be about once per "in game" second regardless of speed
        private const uint FRAME_INDEX_THRESHOLD = 60;

        private static PollutionSolutionConfiguration config = Configuration<PollutionSolutionConfiguration>.Load();

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
            UIHelperBase settingsGroup = helper.AddGroup("Remove");
            settingsGroup.AddCheckbox("Noise Pollution", config.RemoveNoisePollution, (selected) => {
                config.RemoveNoisePollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddCheckbox("Ground Pollution", config.RemoveGroundPollution, (selected) => {
                config.RemoveGroundPollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddCheckbox("Water Pollution", config.RemoveWaterPollution, (selected) => {
                config.RemoveWaterPollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
        }

        // This must remain public
        public class PollutionLogic : ThreadingExtensionBase
        {
            private uint previousUpdatedFrameIndex = 0;

            public override void OnCreated(IThreading threading)
            {
                base.OnCreated(threading);
            }

            public override void OnReleased()
            {
                base.OnReleased();
            }
            public override void OnAfterSimulationFrame()
            {
                // Probably not in a normal game type if the district manager doesn't actually exist
                if (Singleton<DistrictManager>.exists &&
                    // No need to do anything if we're paused
                    !SimulationManager.instance.SimulationPaused &&
                    // Don't do things faster than once every FRAME_INDEX_THRESHOLD
                    SimulationManager.instance.m_currentFrameIndex - previousUpdatedFrameIndex > FRAME_INDEX_THRESHOLD)
                {
                    if (config.RemoveNoisePollution)
                    {
                        ImmaterialResourceManager.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, noiseAmount);
                    }
                    if (config.RemoveGroundPollution)
                    {
                        Singleton<NaturalResourceManager>.instance.AddPollutionDisposeRate(pollutionAmount);
                    }
                    if (config.RemoveWaterPollution)
                    {
                        Singleton<TerrainManager>.instance.WaterSimulation.AddPollutionDisposeRate(pollutionAmount);
                    }
                    previousUpdatedFrameIndex = SimulationManager.instance.m_currentFrameIndex;
                }

                base.OnAfterSimulationTick();
            }
        }
    }
}
