using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class NonCitySite : Site
    {
        public NonCitySite(string name,
                           ResourceType controlType, int controlAmt,
                           ResourceType totalType, int totalAmt)
            : base(name, controlType, controlAmt, totalType, totalAmt)
        {
            IsCity = false;
        }
    }
}
