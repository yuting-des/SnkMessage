using System;
using System.Threading;
using System.Windows;

namespace SnkMessage
{
    internal static class Program
    {
        private static Mutex singleInstance;
        private static EventWaitHandle activateEvent;
        private static RegisteredWaitHandle activateRegistration;

        [STAThread]
        public static void Main()
        {
            bool created;
            singleInstance = new Mutex(true, "SnkMessage.Native.Singleton.v3", out created);
            activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "SnkMessage.Native.Activate.v3");
            if (!created) { activateEvent.Set(); activateEvent.Dispose(); return; }

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var controller = new AppController(app);
            controller.Start();
            activateRegistration=ThreadPool.RegisterWaitForSingleObject(activateEvent,delegate(object state,bool timedOut)
            {
                app.Dispatcher.BeginInvoke(new Action(controller.RestoreTray));
            },null,Timeout.Infinite,false);
            app.Run();
            if(activateRegistration!=null)activateRegistration.Unregister(null);
            activateEvent.Dispose();
            controller.Dispose();
            singleInstance.Dispose();
        }
    }
}
