using System;
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
    public abstract class GameCommandDto
    {
        public int Seq { get; set; }
        public int Seat { get; set; } // Seat Index (0-3) instead of Guid
    }

    public class PlayCardCommandDto : GameCommandDto
    {
        public string? CardId { get; set; }
        public int HandIdx { get; set; }
    }

    public class BuyCardCommandDto : GameCommandDto
    {
        public string? CardId { get; set; }
    }

    public class DeployTroopCommandDto : GameCommandDto
    {
        public int NodeId { get; set; }
    }

    public class DevourCardCommandDto : GameCommandDto
    {
        public string? CardId { get; set; }
        public int HandIdx { get; set; }
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
    }
}
