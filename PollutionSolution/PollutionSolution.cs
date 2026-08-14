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
        private const int NOISE_AMOUNT = Int16.MinValue;

        // This didn't have the same problem as the noise, but it's safer to stay away from the max Int32
        private const int POLLUTION_AMOUNT = Int16.MaxValue;

        // How frequently to remove the pollution, in frames
        // The game will process a different number of frames per wall time depending on the game speed and the ability of the computer
        // At 1x speed, there are 60 frames per second, so 60 here should be about once per "in game" second regardless of speed
        private const uint FRAME_INDEX_THRESHOLD = 60;

        private static PollutionSolutionConfiguration config = Configuration<PollutionSolutionConfiguration>.Load();

        public string Name => "Pollution Solution";

        public string Description => "Enable/Disable Noise, Ground, and Water pollution (by ShadowLink721)";

        public void OnSettingsUI(UIHelperBase helper)
        {
            UIHelperBase settingsGroup = helper.AddGroup("Remove");
            settingsGroup.AddCheckbox("Noise Pollution", config.RemoveNoisePollution, (selected) =>
            {
                config.RemoveNoisePollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddCheckbox("Ground Pollution", config.RemoveGroundPollution, (selected) =>
            {
                config.RemoveGroundPollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddCheckbox("Water Pollution", config.RemoveWaterPollution, (selected) =>
            {
                config.RemoveWaterPollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
        }

        public class PollutionLogic : ThreadingExtensionBase
        {
            private uint previousUpdatedFrameIndex = 0;

            public override void OnAfterSimulationFrame()
            {
                // Ensure we are in actual city gameplay (not an editor or main menu)
                if (!Singleton<ToolManager>.exists ||
                    ToolManager.instance.m_properties == null ||
                    (ToolManager.instance.m_properties.m_mode & ItemClass.Availability.Game) == ItemClass.Availability.None)
                {
                    return;
                }

                // Ensure simulation manager exists, is done loading, and is not paused
                if (!Singleton<SimulationManager>.exists ||
                    !Singleton<LoadingManager>.exists ||
                    !LoadingManager.instance.m_loadingComplete ||
                    SimulationManager.instance.SimulationPaused)
                {
                    return;
                }

                // Don't do things faster than once every FRAME_INDEX_THRESHOLD
                if (SimulationManager.instance.m_currentFrameIndex - previousUpdatedFrameIndex > FRAME_INDEX_THRESHOLD)
                {
                    if (config.RemoveNoisePollution && Singleton<ImmaterialResourceManager>.exists)
                    {
                        ImmaterialResourceManager.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, NOISE_AMOUNT);
                    }
                    if (config.RemoveGroundPollution && Singleton<NaturalResourceManager>.exists)
                    {
                        NaturalResourceManager.instance.AddPollutionDisposeRate(POLLUTION_AMOUNT);
                    }
                    if (config.RemoveWaterPollution && Singleton<TerrainManager>.exists && TerrainManager.instance.WaterSimulation != null)
                    {
                        TerrainManager.instance.WaterSimulation.AddPollutionDisposeRate(POLLUTION_AMOUNT);
                    }
                    previousUpdatedFrameIndex = SimulationManager.instance.m_currentFrameIndex;
                }
            }
        }
    }
}
