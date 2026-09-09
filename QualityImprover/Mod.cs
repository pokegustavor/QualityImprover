using PulsarModLoader;
using QualityImprover.Patches;
namespace QualityImprover
{
    public class Mod : PulsarMod
    {
        public Mod()
        {
            Instance = this;
            //Add Version check for host having the extractor fixes to disable some client side changes
            Events.Instance.ClientModlistRecievedEvent += QualityImprover.Patches.ExtractorFixes.ExtractorFixesHostVersionCheck;
        }
        internal static Mod Instance;
        public override string Version => "2.3.9";

        public override string Author => "pokegustavo, OnHyex";

        public override string ShortDescription => "Adds small quality of life changes and bug fixes";

        public override string Name => "Quality Improver";

        public override string HarmonyIdentifier()
        {
            return "pokegustavo.qualityimprover";
        }
        public override void Unload()
        {
            base.Unload();
            //Removing the version check for host having extractor fixes on unload
            Events.Instance.ClientModlistRecievedEvent -= QualityImprover.Patches.ExtractorFixes.ExtractorFixesHostVersionCheck;
            //Deletes the fix host button on the current playership when unloaded
            ExtractorFixes.FixExtractorForPlayerShipUpdating.DeleteButtonOnUnload();
        }
    }
}
