namespace VeriSolRunner.ExternalTools
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public static class ExternalToolsManager
    {
        private static ILogger logger;

        public static ToolManager Solc { get; private set; }
        public static ToolManager Z3 { get; private set; }
        public static ToolManager Boogie { get; private set; }
        public static ToolManager Corral { get; private set; }

        static ExternalToolsManager()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            logger = loggerFactory.CreateLogger("VeriSol.ExternalToolsManager");

            // Charger tool settings depuis le fichier JSON
            IConfiguration toolSourceConfig = new ConfigurationBuilder()
                .AddJsonFile("toolsourcesettings.json", optional: true, reloadOnChange: true)
                .Build();

            // SOLC - automatiquement téléchargé
            var solcSourceSettings = new ToolSourceSettings();
            toolSourceConfig.GetSection("solc").Bind(solcSourceSettings);
            Solc = new SolcManager(solcSourceSettings);

            // Z3 - installé localement
            Z3 = new DotnetCliToolManager(new ToolSourceSettings
            {
                ToolName = "z3",
                ToolPath = "/usr/bin"
            });

            // Boogie local
            Boogie = new DotnetCliToolManager(new ToolSourceSettings
            {
                ToolName = "boogie",
                ToolPath = "/home/hamoud/Desktop/verisol/bin/Debug/boogie"
            });

            // Corral local
            Corral = new DotnetCliToolManager(new ToolSourceSettings
            {
                ToolName = "corral",
                ToolPath = "/home/hamoud/Desktop/verisol/bin/Debug/corral"
            });
        }

        internal static void Log(string v)
        {
            logger.LogDebug(v);
        }

        public static void EnsureAllExisted()
        {
            Solc.EnsureExisted();
            Z3.EnsureExisted(); // désactivé car déjà installé
            Boogie.EnsureExisted();
            (Boogie as DotnetCliToolManager)?.EnsureLinkedToZ3(Z3);
            Corral.EnsureExisted();
        }
    }
}
