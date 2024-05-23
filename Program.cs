using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace Source_Launcher
{
    internal class Program
    {
        static List<Assembly> loadedAssemblies = new List<Assembly>();
        static string srcFilePath = "";
        static Dictionary<string, object> config = new Dictionary<string, object>();

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            if (args.Length == 0)
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Title = "Select an executable file:";
                dialog.Filter = "Source Executable File|default.src";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    srcFilePath = dialog.FileName;
                    Console.WriteLine("[*] Reading the .src file...");
                    ReadSRCFile(srcFilePath);
                    Console.WriteLine("[*] Checking the main dlls...");
                    CheckFilesAndDirectories(srcFilePath);
                    Console.WriteLine("[*] Loading all the dlls...");
                    LoadDLLs(srcFilePath);
                    Console.WriteLine("[*] Calling \"Init\" method on the \"Application\" class...");
                    ExecuteMethod("Application", "InitWithSRCFile", new object[] { srcFilePath }, BindingFlags.Static | BindingFlags.NonPublic);
                    Console.WriteLine($"[*] Calling \"Main\" method on the \"{config["mainMethodClassName"]}\" class...");
                    ExecuteMethod((string)config["mainMethodClassName"], "Main", new object[] { new string[] { } },
                        BindingFlags.Static | BindingFlags.NonPublic, true);
                }
            }
            else if (args.Length == 1)
            {
                if (File.Exists(args[0]))
                {
                    srcFilePath = args[0];
                    Console.WriteLine("[*] Reading the .src file...");
                    ReadSRCFile(srcFilePath);
                    Console.WriteLine("[*] Checking the main dlls...");
                    CheckFilesAndDirectories(srcFilePath);
                    Console.WriteLine("[*] Loading all the dlls...");
                    LoadDLLs(srcFilePath);
                    Console.WriteLine("[*] Calling \"Init\" method on the \"Application\" class...");
                    ExecuteMethod("Application", "InitWithSRCFile", new object[] { srcFilePath }, BindingFlags.Static | BindingFlags.NonPublic);
                    Console.WriteLine($"[*] Calling \"Main\" method on the \"{config["mainMethodClassName"]}\" class...");
                    ExecuteMethod((string)config["mainMethodClassName"], "Main", new object[] { new string[] { } },
                        BindingFlags.Static | BindingFlags.NonPublic, true);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The specified default.src file path doesn't exists.");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unexpected arguments. Only can be one.");
                Console.ReadKey();
            }
        }

        public static void CheckFilesAndDirectories(string srcFilePath)
        {
            // These dlls are only the main ones, but can be more if the app has more than one project.
            Dictionary<string, string> mainDllsToCheck = new Dictionary<string, string>()
            {
                { "API DLL", Path.Combine(Path.GetDirectoryName(srcFilePath), "data", "API.dll") },
                { "Main App DLL", Path.Combine(Path.GetDirectoryName(srcFilePath), "data", (string)config["mainAppProjName"] + ".dll") },
                { "JSON DLL", Path.Combine(Path.GetDirectoryName(srcFilePath), "data", "Newtonsoft.Json.dll") },
                { "Logger EXE", Path.Combine(Path.GetDirectoryName(srcFilePath), "data", "Logger.exe") }
            };
            foreach (var pair in mainDllsToCheck)
            {
                if (!File.Exists(pair.Value))
                {
                    PrintError($"Error loading or finding the {pair.Key}");
                }
            }
        }

        public static void ReadSRCFile(string srcFilePath)
        {
            // Deserealize the file:
            FileStream fs = new FileStream(srcFilePath, FileMode.Open);
            BinaryFormatter bf = new BinaryFormatter();
            try
            {
                config = (Dictionary<string, object>)bf.Deserialize(fs);
            }
            catch
            {
                PrintError("Error deserealizing the default.src file.");
                Console.ReadKey();
                Environment.Exit(0);
            }
            fs.Close();
        }

        public static void LoadDLLs(string srcFilePath)
        {
            string dataPath = Path.Combine(Path.GetDirectoryName(srcFilePath), "data");

            foreach (string file in Directory.GetFiles(dataPath))
            {
                try
                {
                    loadedAssemblies.Add(Assembly.LoadFile(file));
                }
                catch
                {
                    PrintError($"Error loading the \"{Path.GetFileNameWithoutExtension(file)}\" DLL.");
                }
            }
        }

        public static void ExecuteMethod(string typeName, string methodName, object[] parms, BindingFlags flags = BindingFlags.Default, bool clearConsoleOnCall = false)
        {
            foreach (Assembly assembly in loadedAssemblies)
            {
                Type type = null;
                if (!string.IsNullOrEmpty(typeName)) { type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName); }
                else { type = assembly.GetTypes().FirstOrDefault(t => t.GetMethod(methodName) != null); }

                if (type != null)
                {
                    MethodInfo method = type.GetMethod(methodName, flags);
                    if (method != null)
                    {
                        try
                        {
                            if (clearConsoleOnCall) { Console.Clear(); }
                            method.Invoke(null, parms);
                        }
                        catch (Exception e)
                        {
                            throw e.InnerException;
                        }
                    }
                    break;
                }
            }
            PrintError($"Can't find the \"{methodName}\" method.");
            Console.ReadKey();
        }

        static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
            string assemblyPath = Path.Combine(Path.GetDirectoryName(srcFilePath), "data", assemblyName);

            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            return null;
        }

        static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
