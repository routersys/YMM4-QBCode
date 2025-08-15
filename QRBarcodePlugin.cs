using System.IO;
using System.Reflection;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YMM4QRBarcodePlugin
{
    public class QRBarcodePlugin : IShapePlugin
    {
        private static bool _resolverInitialized = false;
        private static readonly object _lock = new object();

        static QRBarcodePlugin()
        {
            InitializeAssemblyResolver();
        }

        public string Name => "QR・バーコード";

        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            try
            {
                return new QRBarcodeParameter(sharedData);
            }
            catch
            {
                throw;
            }
        }

        private static void InitializeAssemblyResolver()
        {
            lock (_lock)
            {
                if (_resolverInitialized) return;
                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                    _resolverInitialized = true;
                }
                catch
                {
                }
            }
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                var assemblyName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(assemblyName)) return null;

                var targetAssemblies = new[] { "ZXing", "ZXing.Windows.Compatibility" };
                if (!targetAssemblies.Contains(assemblyName)) return null;

                var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(pluginDir)) return null;

                var assemblyPath = Path.Combine(pluginDir, assemblyName + ".dll");

                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                var pluginsDir = Path.Combine(Directory.GetParent(pluginDir)?.FullName ?? "", "Plugins");
                if (Directory.Exists(pluginsDir))
                {
                    var altPath = Path.Combine(pluginsDir, assemblyName + ".dll");
                    if (File.Exists(altPath))
                    {
                        return Assembly.LoadFrom(altPath);
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}