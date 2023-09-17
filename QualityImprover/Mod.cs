using PulsarModLoader;
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Assembly-CSharp")]
namespace QualityImprover
{
    public class Mod : PulsarMod
    {
        public override string Version => "2.3.4";

        public override string Author => "pokegustavo";

        public override string ShortDescription => "Adds small quality of life changes and bug fixes";

        public override string Name => "Quality Improver";

        public override string HarmonyIdentifier()
        {
            return "pokegustavo.qualityimprover";
        }
    }
}
