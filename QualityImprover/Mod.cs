using PulsarModLoader;
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Assembly-CSharp")]
namespace QualityImprover
{
    public class Mod : PulsarMod
    {
        public override string Version => "1.7";

        public override string Author => "pokegustavo";

        public override string ShortDescription => "Adds small quality of life changes and bug fixes";

        public override string Name => "Quality Improver";

        public override string HarmonyIdentifier()
        {
            return "pokegustavo.qualityimprover";
        }
    }
}
