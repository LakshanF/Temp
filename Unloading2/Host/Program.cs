using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Host
{
    public enum ResolveEvents
    {
        NoEvent,
        ExpectedEvent,
    };

    public class TestResolve
    {
        static public ResolveEvents ResolveEvent { get; set; }

        static public Assembly ResolvingTestDefault(AssemblyLoadContext alc, AssemblyName assemblyName)
        {
            throw new ArgumentException("Can you hear me now?");
            if (assemblyName.Name.Contains("TestDefaultLoad") && (ResolveEvent == ResolveEvents.NoEvent))
            {
                ResolveEvent = ResolveEvents.ExpectedEvent;
            }
            return null;
        }

        static public Assembly ResolvingTestIsolated(AssemblyLoadContext alc, AssemblyName assemblyName)
        {
            throw new ArgumentException("Can you hear me now?");
            if (assemblyName.Name.Contains("TestIsolatedLoad") && (ResolveEvent == ResolveEvents.NoEvent))
            {
                ResolveEvent = ResolveEvents.ExpectedEvent;
            }
            return null;
        }

        static public void Assert(ResolveEvents expected, Action action)
        {
            ResolveEvent = ResolveEvents.NoEvent;
            try
            {
                action();
            }
            catch
            {
            }
            finally
            {
                throw new Exception();
            }
        }
    }

    // This is a collectible (unloadable) AssemblyLoadContext that loads the dependencies
    // of the plugin from the plugin's binary directory.
    class HostAssemblyLoadContext : AssemblyLoadContext
    {
        // Resolver of the locations of the assemblies that are dependencies of the
        // main plugin assembly.
        private AssemblyDependencyResolver _resolver;

        public HostAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        public Assembly OnPluginResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            return null;
        }


        // The Load method override causes all the dependencies present in the plugin's binary directory to get loaded
        // into the HostAssemblyLoadContext together with the plugin assembly itself.
        // NOTE: The Interface assembly must not be present in the plugin's binary directory, otherwise we would
        // end up with the assembly being loaded twice. Once in the default context and once in the HostAssemblyLoadContext.
        // The types present on the host and plugin side would then not match even though they would have the same names.
        protected override Assembly Load(AssemblyName name)
        {
            return null;
            string assemblyPath = _resolver.ResolveAssemblyToPath(name);
            if (assemblyPath != null)
            {
                Console.WriteLine($"Loading assembly {assemblyPath} into the HostAssemblyLoadContext");
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

    class Program
    {
        // It is important to mark this method as NoInlining, otherwise the JIT could decide
        // to inline it into the Main method. That could then prevent successful unloading
        // of the plugin because some of the MethodInfo / Type / Plugin.Interface / HostAssemblyLoadContext
        // instances may get lifetime extended beyond the point when the plugin is expected to be
        // unloaded.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ExecuteAndUnload(string assemblyPath, out WeakReference alcWeakRef)
        {
            // Create the unloadable HostAssemblyLoadContext
            var alc = new HostAssemblyLoadContext(assemblyPath);
            //AssemblyLoadContext.Default.Resolving += TestResolve.ResolvingTestDefault;
            alc.Resolving += TestResolve.ResolvingTestIsolated;
            // Create a weak reference to the AssemblyLoadContext that will allow us to detect
            // when the unload completes.
            alcWeakRef = new WeakReference(alc);

            try
            {

                // Load the plugin assembly into the HostAssemblyLoadContext.
                // NOTE: the assemblyPath must be an absolute path.
                //Assembly a = alc.LoadFromAssemblyPath(assemblyPath);
                Assembly a = alc.LoadFromAssemblyName(new AssemblyName("Plugin"));

                // Get the plugin interface by calling the PluginClass.GetInterface method via reflection.
                Type pluginType = a.GetType("Plugin.PluginClass");
                MethodInfo getInterface = pluginType.GetMethod("GetInterface", BindingFlags.Static | BindingFlags.Public);
                Plugin.Interface plugin = (Plugin.Interface)getInterface.Invoke(null, null);

                // Now we can call methods of the plugin using the interface
                string result = plugin.GetMessage();
                Plugin.Version version = plugin.GetVersion();

                Console.WriteLine($"Response from the plugin: GetVersion(): {version}, GetMessage(): {result}");

                // This initiates the unload of the HostAssemblyLoadContext. The actual unloading doesn't happen
                // right away, GC has to kick in later to collect all the stuff.
                alc.Unload();
            }
            catch (FileLoadException ex)
            {
                Console.WriteLine(ex);
            }
            catch (FileNotFoundException ex) 
            {
                Console.WriteLine(ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void Main(string[] args)
        {
            WeakReference hostAlcWeakRef;
            string currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#if DEBUG
            string configName = "Debug";
#else
            string configName = "Release";
#endif
            string pluginFullPath = Path.Combine(currentAssemblyDirectory, $"..\\..\\..\\..\\Plugin\\bin\\{configName}\\netcoreapp3.0\\Plugin.dll");
            ExecuteAndUnload(pluginFullPath, out hostAlcWeakRef);

            // Poll and run GC until the AssemblyLoadContext is unloaded.
            // You don't need to do that unless you want to know when the context
            // got unloaded. You can just leave it to the regular GC.
            for (int i = 0; hostAlcWeakRef.IsAlive && (i < 10); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Console.WriteLine($"Unload success: {!hostAlcWeakRef.IsAlive}");
        }
    }
}
