import json, subprocess, os, urllib.request, urllib.error, sys
GIT=r'C:\Users\Percival\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
env=os.environ.copy();env['GIT_TERMINAL_PROMPT']='0';env['GCM_INTERACTIVE']='Never'
result=subprocess.run([GIT,'credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,capture_output=True,env=env)
credentials=dict(line.split('=',1) for line in result.stdout.splitlines() if '=' in line)
if not credentials.get('password'): print('No existing GitHub Git credential available');sys.exit(2)
def request(method,path,body=None):
 data=None if body is None else json.dumps(body).encode()
 req=urllib.request.Request('https://api.github.com'+path,data=data,method=method,headers={'Authorization':'Bearer '+credentials['password'],'Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28','User-Agent':'SnkMessage-Pages-Setup'})
 try:
  with urllib.request.urlopen(req,timeout=30) as response:
   raw=response.read()
   return response.status,json.loads(raw) if raw else {}
 except urllib.error.HTTPError as error:return error.code,json.loads(error.read())
base='/repos/yuting-des/SnkMessage'
if len(sys.argv)>1 and sys.argv[1]=='enable':
 status,pages=request('GET',base+'/pages')
 if status==404:status,pages=request('POST',base+'/pages',{'source':{'branch':'gh-pages','path':'/'},'build_type':'legacy'})
 elif status==200:status,pages=request('PUT',base+'/pages',{'source':{'branch':'gh-pages','path':'/'},'build_type':'legacy'})
 print(json.dumps({'status':status,'message':pages.get('message'),'html_url':pages.get('html_url'),'source':pages.get('source')}))
else:
 status,repo=request('GET',base)
 print(json.dumps({'status':status,'private':repo.get('private'),'permissions':repo.get('permissions'),'default_branch':repo.get('default_branch')}))
 status,pages=request('GET',base+'/pages')
 print(json.dumps({'pages_status':status,'html_url':pages.get('html_url'),'source':pages.get('source'),'message':pages.get('message'),'build_status':pages.get('status')}))
 status,build=request('GET',base+'/pages/builds/latest')
 print(json.dumps({'latest_build_status':status,'build':build.get('status'),'error':build.get('error')}))
 status,runs=request('GET',base+'/actions/runs?per_page=3')
 print(json.dumps({'workflow_status':status,'runs':[{'id':r['id'],'name':r['name'],'status':r['status'],'conclusion':r['conclusion']} for r in runs.get('workflow_runs',[])]}))
