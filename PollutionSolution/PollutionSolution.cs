using ColossalFramework;
using ICities;
using System;

namespace PollutionSolution {
    /// <summary>
    /// Main mod entry point for the Cities: Skylines "Pollution Solution" mod.
    /// Implements <see cref="IUserMod"/> to define mod metadata and settings UI in the game options menu.
    /// </summary>
    public class PollutionSolution : IUserMod {

        /// <summary>
        /// The disposal amount passed to the water simulation for accelerated water pollution decay.
        /// Using <see cref="Int16.MaxValue"/> (32767) provides a strong clearing rate while avoiding 32-bit overflow.
        /// </summary>
        private const int POLLUTION_AMOUNT = Int16.MaxValue;

        /// <summary>
        /// Simulation frame interval threshold (in simulation ticks).
        /// At 1x game speed (~60 simulation frames/sec), 60 frames corresponds to approximately 1 in-game second.
        /// Throttling logic by simulation frames ensures consistent execution rate across different game speeds.
        /// </summary>
        private const uint FRAME_INDEX_THRESHOLD = 60;

        /// <summary>
        /// Cached mod configuration instance loaded from/saved to XML.
        /// </summary>
        private static PollutionSolutionConfiguration config = Configuration<PollutionSolutionConfiguration>.Load();

        /// <summary>
        /// The display name of the mod shown in the Content Manager and Options menu.
        /// </summary>
        public string Name => "Pollution Solution";

        /// <summary>
        /// A brief description of the mod shown in the Content Manager.
        /// </summary>
        public string Description => "Enable/Disable Noise, Ground, and Water pollution (by ShadowLink721)";

        /// <summary>
        /// Builds the mod options settings interface in the game's Options menu.
        /// Creates toggles and percentage cleanup sliders for Noise, Ground, and Water pollution.
        /// </summary>
        /// <param name="helper">The UI helper provided by the game engine for adding UI controls.</param>
        public void OnSettingsUI(UIHelperBase helper) {
            UIHelperBase settingsGroup = helper.AddGroup("Remove");
            settingsGroup.AddCheckbox("Noise Pollution", config.RemoveNoisePollution, (selected) => {
                config.RemoveNoisePollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddSpace(5);
            settingsGroup.AddSlider("Noise Pollution Cleanup (%)", 0f, 100f, 1f, config.NoisePollutionCleanupPercentage * 100f, (val) => {
                config.NoisePollutionCleanupPercentage = val / 100f;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddSpace(5);
            settingsGroup.AddCheckbox("Ground Pollution", config.RemoveGroundPollution, (selected) => {
                config.RemoveGroundPollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddSpace(5);
            settingsGroup.AddSlider("Ground Pollution Cleanup (%)", 0f, 100f, 1f, config.GroundPollutionCleanupPercentage * 100f, (val) => {
                config.GroundPollutionCleanupPercentage = val / 100f;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddSpace(5);
            settingsGroup.AddCheckbox("Water Pollution", config.RemoveWaterPollution, (selected) => {
                config.RemoveWaterPollution = selected;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
            settingsGroup.AddSpace(5);
            settingsGroup.AddSlider("Water Pollution Cleanup (%)", 0f, 100f, 1f, config.WaterPollutionCleanupPercentage * 100f, (val) => {
                config.WaterPollutionCleanupPercentage = val / 100f;
                Configuration<PollutionSolutionConfiguration>.Save();
            });
        }

        /// <summary>
        /// Threading extension that hooks into the game's simulation cycle to perform periodic pollution cleanup.
        /// </summary>
        public class PollutionLogic : ThreadingExtensionBase {
            /// <summary>
            /// Tracks the last simulation frame index when cleanup was executed to throttle operations.
            /// </summary>
            private uint previousUpdatedFrameIndex = 0;

            /// <summary>
            /// Delegate to check whether a specific resource cell contains positive pollution.
            /// </summary>
            /// <param name="cellIndex">The flat array index into the resource buffer.</param>
            /// <returns><c>true</c> if the cell has pollution; otherwise, <c>false</c>.</returns>
            private delegate bool CheckPollution(int cellIndex);

            /// <summary>
            /// Delegate to clear the pollution value in a specific resource cell.
            /// </summary>
            /// <param name="cellIndex">The flat array index into the resource buffer.</param>
            private delegate void ClearPollution(int cellIndex);

            /// <summary>
            /// Called by the game engine after each simulation frame step.
            /// Executes unthrottled cleanup for noise pollution (preventing visual redraw flicker) and throttled
            /// cleanup for ground and water pollution based on user configuration.
            /// </summary>
            public override void OnAfterSimulationFrame() {
                // Ensure we are in actual city gameplay (not an editor or main menu)
                if (!Singleton<ToolManager>.exists ||
                    ToolManager.instance.m_properties == null ||
                    (ToolManager.instance.m_properties.m_mode & ItemClass.Availability.Game) == ItemClass.Availability.None) {
                    return;
                }

                // Ensure simulation manager exists, is done loading, and is not paused
                if (!Singleton<SimulationManager>.exists ||
                    !Singleton<LoadingManager>.exists ||
                    !LoadingManager.instance.m_loadingComplete ||
                    SimulationManager.instance.SimulationPaused) {
                    return;
                }

                // --- Noise Pollution Cleanup (Slice-Synchronized Scaling) ---
                // ImmaterialResourceManager updates 1 row (z) of the 256x256 grid per simulation tick (frameIndex & 255).
                // By scaling only the active row on each frame, each cell is scaled exactly ONCE per engine update cycle,
                // eliminating exponential compounding decay, scan lines, and visual flicker.
                if (config.RemoveNoisePollution && Singleton<ImmaterialResourceManager>.exists) {
                    var manager = Singleton<ImmaterialResourceManager>.instance;
                    ushort[] localResources;
                    int dummyIndex;
                    manager.CheckLocalResources(UnityEngine.Vector3.zero, out localResources, out dummyIndex);

                    if (localResources != null) {
                        int resolution = ImmaterialResourceManager.RESOURCEGRID_RESOLUTION;
                        int resourceCount = ImmaterialResourceManager.RESOURCE_COUNT;
                        int noiseOffset = (int)ImmaterialResourceManager.Resource.NoisePollution;
                        float volumeMultiplier = 1.0f - config.NoisePollutionCleanupPercentage;

                        if (volumeMultiplier <= 0.0f) {
                            // 100% Cleanup: Zero out all noise cells across the map
                            int totalCells = resolution * resolution;
                            for (int i = 0; i < totalCells; i++) {
                                int cellIndex = i * resourceCount + noiseOffset;
                                if (localResources[cellIndex] > 0) {
                                    localResources[cellIndex] = 0;
                                }
                            }
                            manager.AreaModified(0, 0, resolution - 1, resolution - 1);
                        } else {
                            // Partial Cleanup: Scale only the single row (z) refreshed on this simulation tick
                            int currentZ = (int)(SimulationManager.instance.m_currentFrameIndex & 255u);
                            int rowOffset = currentZ * resolution;

                            for (int x = 0; x < resolution; x++) {
                                int cellIndex = (rowOffset + x) * resourceCount + noiseOffset;
                                if (localResources[cellIndex] > 0) {
                                    localResources[cellIndex] = (ushort)(localResources[cellIndex] * volumeMultiplier);
                                }
                            }
                            manager.AreaModified(0, currentZ, resolution - 1, currentZ);
                        }
                    }
                }

                // Don't do ground and water updates faster than once every FRAME_INDEX_THRESHOLD
                if (SimulationManager.instance.m_currentFrameIndex - previousUpdatedFrameIndex > FRAME_INDEX_THRESHOLD) {
                    // --- Ground Pollution Cleanup ---
                    // Accesses the natural resource cell array (512x512 grid containing ground pollution bytes).
                    // Applies deterministic spatial dither to zero out pollution in X% of polluted cells.
                    if (config.RemoveGroundPollution && Singleton<NaturalResourceManager>.exists) {
                        var manager = Singleton<NaturalResourceManager>.instance;
                        NaturalResourceManager.ResourceCell[] naturalResources = manager.m_naturalResources;

                        if (naturalResources != null) {
                            int resolution = NaturalResourceManager.RESOURCEGRID_RESOLUTION;
                            ApplyStochasticCleanup(
                                resolution,
                                1, // Stride is 1 for flat ResourceCell struct array
                                0, // Offset is 0 (direct struct field access)
                                config.GroundPollutionCleanupPercentage,
                                (idx) => naturalResources[idx].m_pollution > 0,
                                (idx) => naturalResources[idx].m_pollution = 0
                            );
                            manager.AreaModified(0, 0, resolution - 1, resolution - 1);
                        }
                    }

                    // --- Water Pollution Cleanup ---
                    // Water pollution is handled dynamically by fluid simulation worker threads.
                    // We apply a non-linear cubic response curve (x^3) to give fine-grained control at lower slider
                    // percentages before scaling up to full accelerated disposal at 100%.
                    if (config.RemoveWaterPollution && Singleton<TerrainManager>.exists && Singleton<TerrainManager>.instance.WaterSimulation != null) {
                        float cleanupPercentage = config.WaterPollutionCleanupPercentage;
                        if (cleanupPercentage >= 1.0f) {
                            Singleton<TerrainManager>.instance.WaterSimulation.AddPollutionDisposeRate(POLLUTION_AMOUNT);
                        } else if (cleanupPercentage > 0.0f) {
                            float curve = cleanupPercentage * cleanupPercentage * cleanupPercentage;
                            int scaledWater = (int)(POLLUTION_AMOUNT * curve);
                            if (scaledWater == 0) scaledWater = 1;

                            Singleton<TerrainManager>.instance.WaterSimulation.AddPollutionDisposeRate(scaledWater);
                        }
                    }
                    previousUpdatedFrameIndex = SimulationManager.instance.m_currentFrameIndex;
                }
            }

            /// <summary>
            /// Iterates over a 2D resource grid and selectively zeros out pollution cells using a deterministic spatial hash.
            /// </summary>
            /// <param name="gridResolution">The width and height dimension of the 2D grid (e.g. 256 or 512).</param>
            /// <param name="stride">The number of array elements per grid cell (e.g., RESOURCE_COUNT for interleaved buffers, 1 for flat arrays).</param>
            /// <param name="offset">The element offset within the cell stride for the target resource channel.</param>
            /// <param name="cleanupPercentage">The target reduction ratio between 0.0 (0%) and 1.0 (100%).</param>
            /// <param name="hasPollution">Predicate function to test if the cell at the calculated index has positive pollution.</param>
            /// <param name="clearPollution">Action delegate to zero out the pollution value at the calculated index.</param>
            private static void ApplyStochasticCleanup(
                int gridResolution,
                int stride,
                int offset,
                float cleanupPercentage,
                CheckPollution hasPollution,
                ClearPollution clearPollution)
            {
                int totalCells = gridResolution * gridResolution;
                for (int i = 0; i < totalCells; i++) {
                    int cellIndex = i * stride + offset;
                    // Early bailout: skip hash and coordinate math if cell is already clean
                    if (hasPollution(cellIndex)) {
                        int x = i % gridResolution;
                        int z = i / gridResolution;
                        float pixelHash = GetSpatialHash(x, z);
                        if (pixelHash < cleanupPercentage) {
                            clearPollution(cellIndex);
                        }
                    }
                }
            }

            /// <summary>
            /// Generates a stable, flicker-free pseudo-random value between 0.0 and 1.0 for a given 2D coordinate.
            /// </summary>
            /// <param name="x">The X grid coordinate.</param>
            /// <param name="y">The Y (or Z) grid coordinate.</param>
            /// <returns>A deterministic floating-point value in the range [0.0, 1.0).</returns>
            /// <remarks>
            /// This uses the Canonical GLSL Pseudo-Random Number Generator formula popular in GPU shaders.
            /// The magic constants (12.9898, 78.233, 43758.5453123) are chosen to create a high-frequency
            /// sine wave that strips down into chaotic fractional noise, creating a stable visual dither.
            /// Reference: https://thebookofshaders.com/10/
            /// </remarks>
            private static float GetSpatialHash(int x, int y) {
                // A standard high-frequency dot product pseudo-random generator
                // It takes 2 integers and maps them deterministically to a fractional float
                double dot = (x * 12.9898) + (y * 78.233);
                double sin = System.Math.Sin(dot) * 43758.5453123;

                // Equivalent to fract() in shaders (returns only the decimal portion)
                return (float)(sin - System.Math.Floor(sin));
            }

        }
    }
}
