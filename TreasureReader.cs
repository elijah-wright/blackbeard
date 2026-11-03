using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Unreal.Core;
using Unreal.Core.Contracts;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace Blackbeard;

public sealed record TreasurePrize(uint? OpeningPawn, bool? Success, string? PrizeTemplateId, int? Tier);

public sealed record OpenedChest(
    uint Id,
    string Type,
    uint Pawn,
    float OpenedAt,
    (double X, double Y, double Z)? Location,
    TreasurePrize? Prize);

public sealed class TreasureReader : ReplayReader
{
    private readonly Dictionary<uint, uint> _actors = [];
    private readonly Dictionary<uint, uint> _playerIDs = [];
    private readonly Dictionary<uint, (double X, double Y, double Z)> _spawnLocations = [];

    private readonly Dictionary<uint, Chest> _chests = [];
    private readonly Dictionary<uint, TreasurePrize> _prizes = [];

    private float _frameTime;

    public TreasureReader(ILogger? logger = null, ParseMode parseMode = ParseMode.Full)
        : base(logger, parseMode) { }

    public IEnumerable<uint> GetPawnsFromPlayerID(uint playerID) =>
        _playerIDs.Where(kv => kv.Value == playerID).Select(kv => kv.Key);

    public IEnumerable<OpenedChest> GetOpenedChests() =>
        _chests
            .Where(kv => kv.Value is { Searched: true, OpenedBy: not null, OpenedAt: not null })
            .Select(kv => new OpenedChest(
                kv.Key,
                kv.Value.Type,
                kv.Value.OpenedBy!.Value,
                kv.Value.OpenedAt!.Value,
                _spawnLocations.TryGetValue(kv.Key, out var loc) ? loc : null,
                _prizes.GetValueOrDefault(kv.Key)))
            .OrderBy(c => c.OpenedAt);

    public override void ReadDemoFrameIntoPlaybackPackets(FArchive archive)
    {
        var start = archive.Position;
        if (archive.NetworkVersion >= NetworkVersionHistory.HISTORY_MULTIPLE_LEVELS)
            archive.ReadInt32();
        _frameTime = archive.ReadSingle();
        archive.Seek(start, SeekOrigin.Begin);

        base.ReadDemoFrameIntoPlaybackPackets(archive);
    }

    protected override void OnChannelOpened(uint channelIndex, NetworkGUID? actor)
    {
        if (actor?.Value is uint guid)
        {
            _actors[channelIndex] = guid;

            var a = Channels[channelIndex]?.Actor;
            if (a is not null && actor.IsDynamic() && a.Location is { } loc)
                _spawnLocations.TryAdd(guid, ((double)loc.X, (double)loc.Y, (double)loc.Z));
        }

        base.OnChannelOpened(channelIndex, actor);
    }

    protected override void OnExportRead(uint channelIndex, INetFieldExportGroup? exportGroup)
    {
        if (exportGroup is ChestState chest) {
            if (_actors.TryGetValue(channelIndex, out var actor)) {
                var pawn = chest.SearchingPawn is uint p && p != 0 ? p : (uint?)null;

                if (!_chests.TryGetValue(actor, out var c))
                {
                    _chests[actor] = c = new Chest(chest.GetType().Name)
                    {
                        AlreadySearched = chest.bAlreadySearched == true && pawn is null
                    };
                }

                if (pawn is not null)
                    c.Pawn = pawn;

                if (chest.bAlreadySearched == true && !c.Searched && !c.AlreadySearched)
                {
                    c.Searched = true;
                    c.OpenedAt = _frameTime;
                    c.OpenedBy = c.Pawn;
                }
            }
        }


        if (exportGroup is TreasureChest treasure && treasure.SourceContainer is uint source && source != 0)
        {
            _prizes[source] = new TreasurePrize(
                treasure.OpeningPawn,
                treasure.bSuccess,
                treasure.PrizeTemplateId?.AsFString(),
                treasure.Tier?.AsInt32());
        }

        GetPlayerStateFromPawn(channelIndex, exportGroup);
        base.OnExportRead(channelIndex, exportGroup);
    }

    private void GetPlayerStateFromPawn(uint channelIndex, INetFieldExportGroup? exportGroup) {
        if (exportGroup is null || !_actors.TryGetValue(channelIndex, out var actor))
            return;

        var type = exportGroup.GetType();

        if (AsGuid(type.GetProperty("PlayerState")?.GetValue(exportGroup)) is uint ps && ps != 0)
            _playerIDs[actor] = ps;
    }

    private static uint? AsGuid(object? value) => value switch
    {
        null => null,
        uint u => u,
        _ => value.GetType().GetProperty("Value")?.GetValue(value) as uint?,
    };

    private sealed class Chest(string type) {
        public string Type { get; } = type;
        public bool AlreadySearched { get; init; }
        public bool Searched { get; set; }
        public uint? Pawn { get; set; }
        public uint? OpenedBy { get; set; }
        public float? OpenedAt { get; set; }
    }
}