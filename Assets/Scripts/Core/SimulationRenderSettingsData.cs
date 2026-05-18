using UnityEngine;

[System.Serializable]
public class SimulationRenderSettingsData
{
    [Header("地形视图颜色")]
    public Color terrainColorOcean = SimulationConfig.TerrainColorOcean;
    public Color terrainColorLand = SimulationConfig.TerrainColorLand;
    public Color terrainColorBeach = SimulationConfig.TerrainColorBeach;
    public Color terrainColorRiver = SimulationConfig.TerrainColorRiver;
    public Color terrainColorLake = SimulationConfig.TerrainColorLake;

    [Header("温度视图颜色")]
    public Color tempColorBlue = SimulationConfig.TempColorBlue;
    public Color tempColorCyan = SimulationConfig.TempColorCyan;
    public Color tempColorGreen = SimulationConfig.TempColorGreen;
    public Color tempColorYellow = SimulationConfig.TempColorYellow;
    public Color tempColorOrange = SimulationConfig.TempColorOrange;
    public Color tempColorRed = SimulationConfig.TempColorRed;

    [Header("光照视图颜色")]
    public Color lightColorDark = SimulationConfig.LightColorDark;
    public Color lightColorBright = SimulationConfig.LightColorBright;

    [Header("高度视图颜色")]
    public Color altitudeColorDeepWater = SimulationConfig.AltitudeColorDeepWater;
    public Color altitudeColorShallowWater = SimulationConfig.AltitudeColorShallowWater;
    public Color altitudeColorCoast = SimulationConfig.AltitudeColorCoast;
    public Color altitudeColorCoastalPlain = SimulationConfig.AltitudeColorCoastalPlain;
    public Color altitudeColorLowland = SimulationConfig.AltitudeColorLowland;
    public Color altitudeColorHighland = SimulationConfig.AltitudeColorHighland;
    public Color altitudeColorUpland = SimulationConfig.AltitudeColorUpland;
    public Color altitudeColorMountain = SimulationConfig.AltitudeColorMountain;
    public Color altitudeColorSnow = SimulationConfig.AltitudeColorSnow;

    [Header("高度视图参数")]
    public int altitudeContourLevels = SimulationConfig.AltitudeContourLevels;
    public int altitudeCoastBandWidth = SimulationConfig.AltitudeCoastBandWidth;
    [Range(0f, 1f)] public float altitudeContourDarken = SimulationConfig.AltitudeContourDarken;

    [Header("3D 地形法线参数")]
    public int terrainSlopeSampleRadius = SimulationConfig.TerrainSlopeSampleRadius;
    public int terrainMacroReliefRadius = SimulationConfig.TerrainMacroReliefRadius;
    public float terrainNormalStrength = SimulationConfig.TerrainNormalStrength;
    public float terrainLandSlopeNoiseFloor = SimulationConfig.TerrainLandSlopeNoiseFloor;
    public float terrainWaterSlopeNoiseFloor = SimulationConfig.TerrainWaterSlopeNoiseFloor;
    public float terrainLandReliefNoiseFloor = SimulationConfig.TerrainLandReliefNoiseFloor;
    public float terrainWaterReliefNoiseFloor = SimulationConfig.TerrainWaterReliefNoiseFloor;
    public float terrainCurvatureStrength = SimulationConfig.TerrainCurvatureStrength;
    public float terrainHeightContrast = SimulationConfig.TerrainHeightContrast;
    public float terrainHighTintStrength = SimulationConfig.TerrainHighTintStrength;
    public float terrainLowTintStrength = SimulationConfig.TerrainLowTintStrength;
    [Range(0f, 1f)] public float terrainLightAmbient = SimulationConfig.TerrainLightAmbient;
    public float terrainLightDiffuse = SimulationConfig.TerrainLightDiffuse;
    public Vector3 terrainLightDirection = SimulationConfig.TerrainLightDirection;

    [Header("叠加层")]
    [Range(0f, 1f)] public float overlayAlpha = SimulationConfig.OverlayAlpha;

    public void CopyFromSimulationConfig()
    {
        terrainColorOcean = SimulationConfig.TerrainColorOcean;
        terrainColorLand = SimulationConfig.TerrainColorLand;
        terrainColorBeach = SimulationConfig.TerrainColorBeach;
        terrainColorRiver = SimulationConfig.TerrainColorRiver;
        terrainColorLake = SimulationConfig.TerrainColorLake;

        tempColorBlue = SimulationConfig.TempColorBlue;
        tempColorCyan = SimulationConfig.TempColorCyan;
        tempColorGreen = SimulationConfig.TempColorGreen;
        tempColorYellow = SimulationConfig.TempColorYellow;
        tempColorOrange = SimulationConfig.TempColorOrange;
        tempColorRed = SimulationConfig.TempColorRed;

        lightColorDark = SimulationConfig.LightColorDark;
        lightColorBright = SimulationConfig.LightColorBright;

        altitudeColorDeepWater = SimulationConfig.AltitudeColorDeepWater;
        altitudeColorShallowWater = SimulationConfig.AltitudeColorShallowWater;
        altitudeColorCoast = SimulationConfig.AltitudeColorCoast;
        altitudeColorCoastalPlain = SimulationConfig.AltitudeColorCoastalPlain;
        altitudeColorLowland = SimulationConfig.AltitudeColorLowland;
        altitudeColorHighland = SimulationConfig.AltitudeColorHighland;
        altitudeColorUpland = SimulationConfig.AltitudeColorUpland;
        altitudeColorMountain = SimulationConfig.AltitudeColorMountain;
        altitudeColorSnow = SimulationConfig.AltitudeColorSnow;

        altitudeContourLevels = SimulationConfig.AltitudeContourLevels;
        altitudeCoastBandWidth = SimulationConfig.AltitudeCoastBandWidth;
        altitudeContourDarken = SimulationConfig.AltitudeContourDarken;

        terrainSlopeSampleRadius = SimulationConfig.TerrainSlopeSampleRadius;
        terrainMacroReliefRadius = SimulationConfig.TerrainMacroReliefRadius;
        terrainNormalStrength = SimulationConfig.TerrainNormalStrength;
        terrainLandSlopeNoiseFloor = SimulationConfig.TerrainLandSlopeNoiseFloor;
        terrainWaterSlopeNoiseFloor = SimulationConfig.TerrainWaterSlopeNoiseFloor;
        terrainLandReliefNoiseFloor = SimulationConfig.TerrainLandReliefNoiseFloor;
        terrainWaterReliefNoiseFloor = SimulationConfig.TerrainWaterReliefNoiseFloor;
        terrainCurvatureStrength = SimulationConfig.TerrainCurvatureStrength;
        terrainHeightContrast = SimulationConfig.TerrainHeightContrast;
        terrainHighTintStrength = SimulationConfig.TerrainHighTintStrength;
        terrainLowTintStrength = SimulationConfig.TerrainLowTintStrength;
        terrainLightAmbient = SimulationConfig.TerrainLightAmbient;
        terrainLightDiffuse = SimulationConfig.TerrainLightDiffuse;
        terrainLightDirection = SimulationConfig.TerrainLightDirection;
        overlayAlpha = SimulationConfig.OverlayAlpha;
    }

    public int ComputeHash()
    {
        return ComputeHash(
            terrainColorOcean,
            terrainColorLand,
            terrainColorBeach,
            terrainColorRiver,
            terrainColorLake,
            tempColorBlue,
            tempColorCyan,
            tempColorGreen,
            tempColorYellow,
            tempColorOrange,
            tempColorRed,
            lightColorDark,
            lightColorBright,
            altitudeColorDeepWater,
            altitudeColorShallowWater,
            altitudeColorCoast,
            altitudeColorCoastalPlain,
            altitudeColorLowland,
            altitudeColorHighland,
            altitudeColorUpland,
            altitudeColorMountain,
            altitudeColorSnow,
            altitudeContourLevels,
            altitudeCoastBandWidth,
            altitudeContourDarken,
            terrainSlopeSampleRadius,
            terrainMacroReliefRadius,
            terrainNormalStrength,
            terrainLandSlopeNoiseFloor,
            terrainWaterSlopeNoiseFloor,
            terrainLandReliefNoiseFloor,
            terrainWaterReliefNoiseFloor,
            terrainCurvatureStrength,
            terrainHeightContrast,
            terrainHighTintStrength,
            terrainLowTintStrength,
            terrainLightAmbient,
            terrainLightDiffuse,
            terrainLightDirection,
            overlayAlpha);
    }

    public static int ComputeSimulationConfigHash()
    {
        return ComputeHash(
            SimulationConfig.TerrainColorOcean,
            SimulationConfig.TerrainColorLand,
            SimulationConfig.TerrainColorBeach,
            SimulationConfig.TerrainColorRiver,
            SimulationConfig.TerrainColorLake,
            SimulationConfig.TempColorBlue,
            SimulationConfig.TempColorCyan,
            SimulationConfig.TempColorGreen,
            SimulationConfig.TempColorYellow,
            SimulationConfig.TempColorOrange,
            SimulationConfig.TempColorRed,
            SimulationConfig.LightColorDark,
            SimulationConfig.LightColorBright,
            SimulationConfig.AltitudeColorDeepWater,
            SimulationConfig.AltitudeColorShallowWater,
            SimulationConfig.AltitudeColorCoast,
            SimulationConfig.AltitudeColorCoastalPlain,
            SimulationConfig.AltitudeColorLowland,
            SimulationConfig.AltitudeColorHighland,
            SimulationConfig.AltitudeColorUpland,
            SimulationConfig.AltitudeColorMountain,
            SimulationConfig.AltitudeColorSnow,
            SimulationConfig.AltitudeContourLevels,
            SimulationConfig.AltitudeCoastBandWidth,
            SimulationConfig.AltitudeContourDarken,
            SimulationConfig.TerrainSlopeSampleRadius,
            SimulationConfig.TerrainMacroReliefRadius,
            SimulationConfig.TerrainNormalStrength,
            SimulationConfig.TerrainLandSlopeNoiseFloor,
            SimulationConfig.TerrainWaterSlopeNoiseFloor,
            SimulationConfig.TerrainLandReliefNoiseFloor,
            SimulationConfig.TerrainWaterReliefNoiseFloor,
            SimulationConfig.TerrainCurvatureStrength,
            SimulationConfig.TerrainHeightContrast,
            SimulationConfig.TerrainHighTintStrength,
            SimulationConfig.TerrainLowTintStrength,
            SimulationConfig.TerrainLightAmbient,
            SimulationConfig.TerrainLightDiffuse,
            SimulationConfig.TerrainLightDirection,
            SimulationConfig.OverlayAlpha);
    }

    private static int ComputeHash(
        Color terrainColorOcean,
        Color terrainColorLand,
        Color terrainColorBeach,
        Color terrainColorRiver,
        Color terrainColorLake,
        Color tempColorBlue,
        Color tempColorCyan,
        Color tempColorGreen,
        Color tempColorYellow,
        Color tempColorOrange,
        Color tempColorRed,
        Color lightColorDark,
        Color lightColorBright,
        Color altitudeColorDeepWater,
        Color altitudeColorShallowWater,
        Color altitudeColorCoast,
        Color altitudeColorCoastalPlain,
        Color altitudeColorLowland,
        Color altitudeColorHighland,
        Color altitudeColorUpland,
        Color altitudeColorMountain,
        Color altitudeColorSnow,
        int altitudeContourLevels,
        int altitudeCoastBandWidth,
        float altitudeContourDarken,
        int terrainSlopeSampleRadius,
        int terrainMacroReliefRadius,
        float terrainNormalStrength,
        float terrainLandSlopeNoiseFloor,
        float terrainWaterSlopeNoiseFloor,
        float terrainLandReliefNoiseFloor,
        float terrainWaterReliefNoiseFloor,
        float terrainCurvatureStrength,
        float terrainHeightContrast,
        float terrainHighTintStrength,
        float terrainLowTintStrength,
        float terrainLightAmbient,
        float terrainLightDiffuse,
        Vector3 terrainLightDirection,
        float overlayAlpha)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + terrainColorOcean.GetHashCode();
            hash = hash * 31 + terrainColorLand.GetHashCode();
            hash = hash * 31 + terrainColorBeach.GetHashCode();
            hash = hash * 31 + terrainColorRiver.GetHashCode();
            hash = hash * 31 + terrainColorLake.GetHashCode();
            hash = hash * 31 + tempColorBlue.GetHashCode();
            hash = hash * 31 + tempColorCyan.GetHashCode();
            hash = hash * 31 + tempColorGreen.GetHashCode();
            hash = hash * 31 + tempColorYellow.GetHashCode();
            hash = hash * 31 + tempColorOrange.GetHashCode();
            hash = hash * 31 + tempColorRed.GetHashCode();
            hash = hash * 31 + lightColorDark.GetHashCode();
            hash = hash * 31 + lightColorBright.GetHashCode();
            hash = hash * 31 + altitudeColorDeepWater.GetHashCode();
            hash = hash * 31 + altitudeColorShallowWater.GetHashCode();
            hash = hash * 31 + altitudeColorCoast.GetHashCode();
            hash = hash * 31 + altitudeColorCoastalPlain.GetHashCode();
            hash = hash * 31 + altitudeColorLowland.GetHashCode();
            hash = hash * 31 + altitudeColorHighland.GetHashCode();
            hash = hash * 31 + altitudeColorUpland.GetHashCode();
            hash = hash * 31 + altitudeColorMountain.GetHashCode();
            hash = hash * 31 + altitudeColorSnow.GetHashCode();
            hash = hash * 31 + altitudeContourLevels;
            hash = hash * 31 + altitudeCoastBandWidth;
            hash = hash * 31 + altitudeContourDarken.GetHashCode();
            hash = hash * 31 + terrainSlopeSampleRadius;
            hash = hash * 31 + terrainMacroReliefRadius;
            hash = hash * 31 + terrainNormalStrength.GetHashCode();
            hash = hash * 31 + terrainLandSlopeNoiseFloor.GetHashCode();
            hash = hash * 31 + terrainWaterSlopeNoiseFloor.GetHashCode();
            hash = hash * 31 + terrainLandReliefNoiseFloor.GetHashCode();
            hash = hash * 31 + terrainWaterReliefNoiseFloor.GetHashCode();
            hash = hash * 31 + terrainCurvatureStrength.GetHashCode();
            hash = hash * 31 + terrainHeightContrast.GetHashCode();
            hash = hash * 31 + terrainHighTintStrength.GetHashCode();
            hash = hash * 31 + terrainLowTintStrength.GetHashCode();
            hash = hash * 31 + terrainLightAmbient.GetHashCode();
            hash = hash * 31 + terrainLightDiffuse.GetHashCode();
            hash = hash * 31 + terrainLightDirection.GetHashCode();
            hash = hash * 31 + overlayAlpha.GetHashCode();
            return hash;
        }
    }

    public void ApplyToSimulationConfig()
    {
        SimulationConfig.TerrainColorOcean = terrainColorOcean;
        SimulationConfig.TerrainColorLand = terrainColorLand;
        SimulationConfig.TerrainColorBeach = terrainColorBeach;
        SimulationConfig.TerrainColorRiver = terrainColorRiver;
        SimulationConfig.TerrainColorLake = terrainColorLake;

        SimulationConfig.TempColorBlue = tempColorBlue;
        SimulationConfig.TempColorCyan = tempColorCyan;
        SimulationConfig.TempColorGreen = tempColorGreen;
        SimulationConfig.TempColorYellow = tempColorYellow;
        SimulationConfig.TempColorOrange = tempColorOrange;
        SimulationConfig.TempColorRed = tempColorRed;

        SimulationConfig.LightColorDark = lightColorDark;
        SimulationConfig.LightColorBright = lightColorBright;

        SimulationConfig.AltitudeColorDeepWater = altitudeColorDeepWater;
        SimulationConfig.AltitudeColorShallowWater = altitudeColorShallowWater;
        SimulationConfig.AltitudeColorCoast = altitudeColorCoast;
        SimulationConfig.AltitudeColorCoastalPlain = altitudeColorCoastalPlain;
        SimulationConfig.AltitudeColorLowland = altitudeColorLowland;
        SimulationConfig.AltitudeColorHighland = altitudeColorHighland;
        SimulationConfig.AltitudeColorUpland = altitudeColorUpland;
        SimulationConfig.AltitudeColorMountain = altitudeColorMountain;
        SimulationConfig.AltitudeColorSnow = altitudeColorSnow;

        SimulationConfig.AltitudeContourLevels = Mathf.Max(2, altitudeContourLevels);
        SimulationConfig.AltitudeCoastBandWidth = Mathf.Max(1, altitudeCoastBandWidth);
        SimulationConfig.AltitudeContourDarken = Mathf.Clamp01(altitudeContourDarken);

        SimulationConfig.TerrainSlopeSampleRadius = Mathf.Max(1, terrainSlopeSampleRadius);
        SimulationConfig.TerrainMacroReliefRadius = Mathf.Max(SimulationConfig.TerrainSlopeSampleRadius + 1, terrainMacroReliefRadius);
        SimulationConfig.TerrainNormalStrength = Mathf.Max(0f, terrainNormalStrength);
        SimulationConfig.TerrainLandSlopeNoiseFloor = Mathf.Clamp(terrainLandSlopeNoiseFloor, 0f, 1f);
        SimulationConfig.TerrainWaterSlopeNoiseFloor = Mathf.Clamp(terrainWaterSlopeNoiseFloor, 0f, 1f);
        SimulationConfig.TerrainLandReliefNoiseFloor = Mathf.Clamp(terrainLandReliefNoiseFloor, 0f, 1f);
        SimulationConfig.TerrainWaterReliefNoiseFloor = Mathf.Clamp(terrainWaterReliefNoiseFloor, 0f, 1f);
        SimulationConfig.TerrainCurvatureStrength = Mathf.Max(0f, terrainCurvatureStrength);
        SimulationConfig.TerrainHeightContrast = Mathf.Max(0f, terrainHeightContrast);
        SimulationConfig.TerrainHighTintStrength = Mathf.Max(0f, terrainHighTintStrength);
        SimulationConfig.TerrainLowTintStrength = Mathf.Max(0f, terrainLowTintStrength);
        SimulationConfig.TerrainLightAmbient = Mathf.Clamp01(terrainLightAmbient);
        SimulationConfig.TerrainLightDiffuse = Mathf.Max(0f, terrainLightDiffuse);
        SimulationConfig.OverlayAlpha = Mathf.Clamp01(overlayAlpha);

        Vector3 lightDirection = terrainLightDirection;
        if (lightDirection.sqrMagnitude < 0.0001f)
            lightDirection = new Vector3(-0.91f, -0.33f, 0.24f);
        SimulationConfig.TerrainLightDirection = lightDirection.normalized;

        CellRenderer.SetTerrainLightDirection(SimulationConfig.TerrainLightDirection);
        CellRenderer.RefreshRenderingSettings();
    }
}