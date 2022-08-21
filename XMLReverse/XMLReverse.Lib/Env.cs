using System;
using System.Diagnostics;
using System.IO;

namespace XMLReverse.Lib
{
    public static class Env
    {
        public static string GetAppFolder()
        {
            var root = AppContext.BaseDirectory;
            var tmp = Path.Combine("bin", "Debug", "net6.0");
            root = root.Replace(tmp, string.Empty);
            root = Path.GetFullPath(root)[..^1];
            return root;
        }

        public static string Combine(string root, params string[] args)
        {
            var sub = Path.Combine(args);
            var dir = Path.Combine(root, sub);
            dir = Path.GetFullPath(dir);
            return dir;
        }

        public static void Execute(string file)
        {
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        }
    }
}