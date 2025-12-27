using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities.Map
{
    public class CitySite : Site
    {
        public CitySite(string name,
                        ResourceType controlType, int controlAmt,
                        ResourceType totalType, int totalAmt)
            : base(name, controlType, controlAmt, totalType, totalAmt)
        {
            IsCity = true;
        }
    }
}


