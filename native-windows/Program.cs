using System;
using System.Threading;
using System.Windows;

namespace SnkMessage
{
    internal static class Program
    {
        private static Mutex singleInstance;

        [STAThread]
        public static void Main()
        {
            bool created;
            singleInstance = new Mutex(true, "SnkMessage.Native.Singleton", out created);
            if (!created) return;

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var controller = new AppController(app);
            controller.Start();
            app.Run();
            controller.Dispose();
            singleInstance.Dispose();
        }
    }
}
