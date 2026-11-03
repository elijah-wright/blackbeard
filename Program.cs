using System.Text.Json;
using System.Text.Json.Serialization;
using Blackbeard;

const string ParserVersion = "0.0.1";

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: blackbeard <path-to-replay>");
    return 2;
}

try {
    var reader = new TreasureReader();
    var replay = reader.ReadReplay(args[0]);

    var gameData = replay.GameData;
    if (gameData is null) {
        throw new Exception("no game data");
    }
    var header = replay.Header;
    if (header is null) {
        throw new Exception("no header");
    }
    uint? recorderId = gameData.RecorderId is { } recorder ? Convert.ToUInt32(recorder) : null;
    if (recorderId is not uint rs) {
        throw new Exception("no recorder ID");
    }
    var recorderPawns = reader.GetPawnsFromPlayerID(rs).ToHashSet();
    if (recorderPawns.Count == 0) {
        throw new Exception("no recorder pawns");
    }

    var chests = reader.GetOpenedChests()
        .Where(c => recorderPawns.Contains(c.Pawn))
        .Select(c => new
        {
            c.Id,
            c.Type,
            c.Pawn,
            OpenedAt = Math.Round(c.OpenedAt, 3),
            Location = c.Location is { } l ? new { l.X, l.Y, l.Z } : null,
            c.Prize,
        })
        .ToList();

    var output = new
    {
        ParserVersion,
        gameData.GameSessionId,
        header.Branch,
        header.Changelist,
        gameData.CurrentPlaylist,
        gameData.UtcTimeStartedMatch,
        RecorderId = rs,
        RecorderPawns = recorderPawns.Order(),
        ChestsOpened = chests.Count,
        Chests = chests,
    };

    Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    }));

    return 0;
}
catch (Exception e) {
    Console.Error.WriteLine(e);
    return 1;
}