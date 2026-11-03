using System.Text;
using Unreal.Core;
using Unreal.Core.Attributes;
using Unreal.Core.Contracts;
using Unreal.Core.Models.Enums;

namespace Blackbeard;

public sealed class RawBits : IProperty
{
    public byte[] Bytes { get; private set; } = Array.Empty<byte>();
    public int BitCount { get; private set; }

    public void Serialize(NetBitReader reader)
    {
        BitCount = reader.GetBitsLeft();
        Bytes = reader.ReadBits(BitCount).ToArray();
    }

    public int? AsInt32() =>
        BitCount == 32 && Bytes.Length >= 4 ? BitConverter.ToInt32(Bytes, 0) : null;

    public string? AsFString()
    {
        for (var offset = 0; offset + 4 <= Bytes.Length; offset++)
        {
            var length = BitConverter.ToInt32(Bytes, offset);
            var start = offset + 4;
            if (length < 2 || start + length > Bytes.Length || Bytes[start + length - 1] != 0)
                continue;

            var chars = Bytes.AsSpan(start, length - 1);
            if (chars.ToArray().All(b => b >= 0x20 && b < 0x7F))
                return Encoding.ASCII.GetString(chars);
        }
        return null;
    }

    public IEnumerable<string> Strings(int minLength = 4)
    {
        var found = new HashSet<string>();
        for (var offset = 0; offset < 8; offset++)
        {
            var sb = new StringBuilder();
            for (var bit = offset; bit + 8 <= BitCount; bit += 8)
            {
                var b = 0;
                for (var i = 0; i < 8; i++)
                    b |= ((Bytes[(bit + i) >> 3] >> ((bit + i) & 7)) & 1) << i;

                if (b >= 0x20 && b < 0x7F)
                {
                    sb.Append((char)b);
                }
                else
                {
                    if (sb.Length >= minLength) found.Add(sb.ToString());
                    sb.Clear();
                }
            }
            if (sb.Length >= minLength) found.Add(sb.ToString());
        }
        return found;
    }
}

public abstract class ChestState : INetFieldExportGroup
{
    [NetFieldExport("bAlreadySearched", RepLayoutCmdType.PropertyBool)]
    public bool? bAlreadySearched { get; set; }

    [NetFieldExport("SearchingPawn", RepLayoutCmdType.PropertyObject)]
    public uint? SearchingPawn { get; set; }
}

[NetFieldExportGroup("/Game/Building/ActorBlueprints/Containers/Tiered_Chest_Athena.Tiered_Chest_Athena_C", ParseMode.Full)]
public sealed class TieredChest : ChestState { }

[NetFieldExportGroup("/Game/Building/ActorBlueprints/Containers/AlwaysSpawn_NormalChest.AlwaysSpawn_NormalChest_C", ParseMode.Full)]
public sealed class AlwaysSpawnNormalChest : ChestState { }

[NetFieldExportGroup("/Game/Building/ActorBlueprints/Containers/AlwaysSpawn_RareChest.AlwaysSpawn_RareChest_C", ParseMode.Full)]
public sealed class AlwaysSpawnRareChest : ChestState { }

[NetFieldExportGroup("/Game/Athena/Items/ForagedItems/BuriedChest/B_Athena_BuriedChest_Chest.B_Athena_BuriedChest_Chest_C", ParseMode.Full)]
public sealed class BuriedChest : ChestState { }

[NetFieldExportGroup("/DelilahTreasureHuntGameplay/Gameplay/Actors/BGA_DelilahTreasureHuntAnimatedChest.BGA_DelilahTreasureHuntAnimatedChest_C", ParseMode.Full)]
public sealed class TreasureChest : INetFieldExportGroup
{
    [NetFieldExport("OpeningPawn", RepLayoutCmdType.PropertyObject)]
    public uint? OpeningPawn { get; set; }

    [NetFieldExport("SourceContainer", RepLayoutCmdType.PropertyObject)]
    public uint? SourceContainer { get; set; }

    [NetFieldExport("bSuccess", RepLayoutCmdType.PropertyBool)]
    public bool? bSuccess { get; set; }

    [NetFieldExport("PrizeTemplateId", RepLayoutCmdType.Property)]
    public RawBits? PrizeTemplateId { get; set; }

    [NetFieldExport("Tier", RepLayoutCmdType.Property)]
    public RawBits? Tier { get; set; }

    [NetFieldExport("TierData", RepLayoutCmdType.PropertyObject)]
    public uint? TierData { get; set; }
}