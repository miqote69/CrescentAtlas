using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Lumina.Excel.Sheets;

namespace CrescentAtlas.Runtime;

public sealed class OccultCrescentContext(IClientState clientState, IDataManager dataManager, IPluginLog log)
{
    public uint TerritoryId => clientState.TerritoryType;

    public string TerritoryName
    {
        get
        {
            try
            {
                return dataManager.GetExcelSheet<TerritoryType>()
                    .TryGetRow(clientState.TerritoryType, out var territory)
                    ? territory.PlaceName.Value.Name.ToString()
                    : string.Empty;
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Failed to resolve territory name for {TerritoryId}.", clientState.TerritoryType);
                return string.Empty;
            }
        }
    }

    public static unsafe bool IsActive()
    {
        try
        {
            return PublicContentOccultCrescent.GetInstance() != null;
        }
        catch
        {
            return false;
        }
    }

    public static unsafe OccultCrescentInstanceSnapshot ReadInstanceSnapshot()
    {
        try
        {
            var instance = PublicContentOccultCrescent.GetInstance();
            if (instance == null)
                return OccultCrescentInstanceSnapshot.Empty;

            var contentDirector = (ContentDirector*)instance;
            var secondsLeft = contentDirector->ContentTimeLeft;
            return new OccultCrescentInstanceSnapshot(
                $"0x{(nuint)instance:X}",
                float.IsFinite(secondsLeft) && secondsLeft > 0.0f
                    ? secondsLeft
                    : null);
        }
        catch
        {
            return OccultCrescentInstanceSnapshot.Empty;
        }
    }
}
