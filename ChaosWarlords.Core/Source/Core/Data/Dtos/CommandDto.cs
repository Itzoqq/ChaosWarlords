using System.Text.Json.Serialization;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "t")]
    [JsonDerivedType(typeof(PlayCardCommandDto), typeDiscriminator: "play")]
    [JsonDerivedType(typeof(BuyCardCommandDto), typeDiscriminator: "buy")]
    [JsonDerivedType(typeof(DeployTroopCommandDto), typeDiscriminator: "deploy")]
    [JsonDerivedType(typeof(EndTurnCommandDto), typeDiscriminator: "end")]
    [JsonDerivedType(typeof(DevourCardCommandDto), typeDiscriminator: "devour")]
    [JsonDerivedType(typeof(CancelActionCommandDto), typeDiscriminator: "cancel")]
    [JsonDerivedType(typeof(ToggleMarketCommandDto), typeDiscriminator: "market")]
    [JsonDerivedType(typeof(SwitchModeCommandDto), typeDiscriminator: "mode")]
    [JsonDerivedType(typeof(StartAssassinateCommandDto), typeDiscriminator: "s_ash")]
    [JsonDerivedType(typeof(StartReturnSpyCommandDto), typeDiscriminator: "s_ret")]
    [JsonDerivedType(typeof(ResolveSpyCommandDto), typeDiscriminator: "res_spy")]
    [JsonDerivedType(typeof(AssassinateCommandDto), typeDiscriminator: "ash")]
    [JsonDerivedType(typeof(ReturnTroopCommandDto), typeDiscriminator: "ret")]
    [JsonDerivedType(typeof(SupplantCommandDto), typeDiscriminator: "supp")]
    [JsonDerivedType(typeof(PlaceSpyCommandDto), typeDiscriminator: "spy")]
    [JsonDerivedType(typeof(MoveTroopCommandDto), typeDiscriminator: "move")]
    [JsonDerivedType(typeof(ActionCompletedCommandDto), typeDiscriminator: "done")]
    [JsonDerivedType(typeof(PromoteCommandDto), typeDiscriminator: "promote")]
    [JsonDerivedType(typeof(DiscardCardCommandDto), typeDiscriminator: "discard")]
    [JsonDerivedType(typeof(ReturnOwnSpyCommandDto), typeDiscriminator: "ret_own_spy")]
    [JsonDerivedType(typeof(PlayFromMarketCommandDto), typeDiscriminator: "play_market")]
    [JsonDerivedType(typeof(SelectOpponentCommandDto), typeDiscriminator: "select_opponent")]
    public abstract class GameCommandDto
    {
        public int Seq { get; set; }
        public int Seat { get; set; } // Seat Index (0-3) instead of Guid
    }

    public class PlayCardCommandDto : GameCommandDto
    {
        public string? CardId { get; set; }
        // Identifies the specific card copy (Card.RuntimeId), disambiguating duplicate
        // copies of the same card definition - CardId alone can't. Additive alongside
        // CardId/HandIdx so older replay JSON without it still loads (falls back to the
        // CardId-then-HandIdx chain below).
        public Guid? CardRuntimeId { get; set; }
        public int HandIdx { get; set; }
    }

    public class BuyCardCommandDto : GameCommandDto
    {
        public string? CardId { get; set; }
        public Guid? CardRuntimeId { get; set; }
    }

    public class DeployTroopCommandDto : GameCommandDto
    {
        public int NodeId { get; set; }
    }

    public class DevourCardCommandDto : GameCommandDto
    {
        public string? CardId { get; set; }
        public Guid? CardRuntimeId { get; set; }
        public int HandIdx { get; set; }
        public string? Location { get; set; } // "Hand", "Market"
        public string? SourceCardId { get; set; }
        public Guid? SourceCardRuntimeId { get; set; }
    }

    public class EndTurnCommandDto : GameCommandDto { }

    public class CancelActionCommandDto : GameCommandDto { }

    public class ToggleMarketCommandDto : GameCommandDto { }

    public class SwitchModeCommandDto : GameCommandDto { }

    public class StartAssassinateCommandDto : GameCommandDto { }

    public class StartReturnSpyCommandDto : GameCommandDto { }

    public class ResolveSpyCommandDto : GameCommandDto
    {
        public int SiteId { get; set; }
        public string? Color { get; set; }
        public string? CardId { get; set; }
    }

    public class AssassinateCommandDto : GameCommandDto
    {
        public int NodeId { get; set; }
        public string? CardId { get; set; }
        public string? DevourCardId { get; set; }
    }

    public class ReturnTroopCommandDto : GameCommandDto
    {
        public int NodeId { get; set; }
        public string? CardId { get; set; }
    }

    public class SupplantCommandDto : GameCommandDto
    {
        public int NodeId { get; set; }
        public string? CardId { get; set; }
        public string? DevourCardId { get; set; }
    }

    public class PlaceSpyCommandDto : GameCommandDto
    {
        public int SiteId { get; set; }
        public string? CardId { get; set; }
    }

    public class MoveTroopCommandDto : GameCommandDto
    {
        public int SrcId { get; set; }
        public int DestId { get; set; }
        public string? CardId { get; set; }
    }

    public class ActionCompletedCommandDto : GameCommandDto { }

    public class PromoteCommandDto : GameCommandDto
    {
        public string? CardId { get; set; }

        // Defaults to false, matching PromoteCommand.IsChainedEffect's default - old replay
        // JSON files recorded before this field existed deserialize with this missing and
        // System.Text.Json defaults it to false, which is exactly the old (only) behavior.
        public bool IsChainedEffect { get; set; }
    }

    public class DiscardCardCommandDto : GameCommandDto
    {
        public string? PlayerColor { get; set; }
        public string? CardId { get; set; }
    }

    public class ReturnOwnSpyCommandDto : GameCommandDto
    {
        public int SiteId { get; set; }
        public string? CardId { get; set; }
    }

    public class PlayFromMarketCommandDto : GameCommandDto
    {
        public Guid MarketCardRuntimeId { get; set; }
        public string? MarketCardId { get; set; }
    }

    public class SelectOpponentCommandDto : GameCommandDto
    {
        public string? TargetPlayerColor { get; set; }
    }
}
