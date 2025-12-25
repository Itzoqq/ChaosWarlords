using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
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
