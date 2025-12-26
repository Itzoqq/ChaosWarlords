using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Entities
{
    public class StartingSite : NonCitySite
    {
        public StartingSite(string name,
                           ResourceType controlType, int controlAmt,
                           ResourceType totalType, int totalAmt)
            : base(name, controlType, controlAmt, totalType, totalAmt)
        {
        }
    }
}
