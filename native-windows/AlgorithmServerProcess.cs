using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SnkMessage
{
    internal sealed class AlgorithmServerProcess:IDisposable
    {
        private static readonly HttpClient HealthClient=new HttpClient();
        private readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        private Process process;
        internal Uri Endpoint { get; }=new Uri("http://127.0.0.1:8787/v1/generate");

        internal async Task EnsureStartedAsync(CancellationToken cancellationToken)
        {
            if(await IsHealthyAsync(cancellationToken))return;
            await gate.WaitAsync(cancellationToken);
            try
            {
                if(await IsHealthyAsync(cancellationToken))return;
                StopOwnedProcess();
                string script=FindServerScript();
                if(script==null)throw new AiServiceException("未找到本地算法服务文件，请重新安装 SnkMessage。");
                string node=FindNodeExecutable();
                var start=new ProcessStartInfo{FileName=node,WorkingDirectory=Path.GetDirectoryName(script),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                start.ArgumentList.Add(script);
                string key=CredentialStore.ReadApiKey();
                if(!String.IsNullOrWhiteSpace(key))start.Environment["OPENROUTER_API_KEY"]=key;
                process=new Process{StartInfo=start,EnableRaisingEvents=true};
                process.OutputDataReceived+=delegate{};process.ErrorDataReceived+=delegate{};
                try{process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();}
                catch(Exception error){StopOwnedProcess();throw new AiServiceException("无法启动本地算法服务。请确认已安装 Node.js 20 或更高版本。",error);}
                for(int i=0;i<40;i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(process.HasExited)throw new AiServiceException("本地算法服务启动失败，请检查 API 设置。");
                    if(await IsHealthyAsync(cancellationToken))return;
                    await Task.Delay(100,cancellationToken);
                }
                throw new AiServiceException("本地算法服务启动超时。");
            }
            finally{gate.Release();}
        }

        internal void Restart(){StopOwnedProcess();}

        private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(350);
                using HttpResponseMessage response=await HealthClient.GetAsync("http://127.0.0.1:8787/health",timeout.Token);
                return response.IsSuccessStatusCode;
            }
            catch{return false;}
        }

        private static string FindServerScript()
        {
            string baseDirectory=AppContext.BaseDirectory;
            string[] candidates={Path.Combine(baseDirectory,"algorithm-server","server.mjs"),Path.GetFullPath(Path.Combine(baseDirectory,"..","algorithm-server","server.mjs")),Path.Combine(Environment.CurrentDirectory,"algorithm-server","server.mjs")};
            foreach(string candidate in candidates)if(File.Exists(candidate))return candidate;
            return null;
        }

        private static string FindNodeExecutable()
        {
            string configured=Environment.GetEnvironmentVariable("SNKMESSAGE_NODE_PATH");
            if(!String.IsNullOrWhiteSpace(configured))return configured;
            string bundled=Path.Combine(AppContext.BaseDirectory,"runtime","node.exe");
            return File.Exists(bundled)?bundled:"node";
        }

        private void StopOwnedProcess()
        {
            if(process==null)return;
            try{if(!process.HasExited)process.Kill(true);}catch{}
            process.Dispose();process=null;
        }

        public void Dispose(){StopOwnedProcess();gate.Dispose();}
    }

    internal sealed class LocalAlgorithmAiService:IAiService,IDisposable
    {
        private readonly AlgorithmServerProcess host=new AlgorithmServerProcess();
        private readonly RemoteAiService remote;
        internal LocalAlgorithmAiService(){remote=new RemoteAiService(host.Endpoint);}
        public async Task<AiResult> GenerateAsync(AiRequest request,CancellationToken cancellationToken)
        {
            await host.EnsureStartedAsync(cancellationToken);
            return await remote.GenerateAsync(request,cancellationToken);
        }
        internal void ReloadConfiguration(){host.Restart();}
        public void Dispose(){host.Dispose();}
    }
}
