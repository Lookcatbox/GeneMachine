#include<bits/stdc++.h>
#define N 2000010
#define mod 1000000007
#define V 2000000
using namespace std;
int n,m,p[N],vis[N],f[N];
int cnt,len[N],pri[N],pn;
int tmi[N],ans;
int _tmi[N];

int qpow(int a1,int a2) {
	int res=1;
	while(a2) {
		if(a2&1) res=1ll*res*a1%mod;
		a1=1ll*a1*a1%mod;
		a2>>=1; 
	} return res;
}
void calc() {
	for(int i=1; i<=V; i++) {
//		cout<<i<<endl;
		int sum=0;
		int t=i;
		for(int j=1; j<=pn; j++) tmi[j]=0;
		for(int j=1; j<=pn; j++) {
//			cout<<"!"<<pri[j]<<endl;
			if(t%pri[j]==0) {
				while(t%pri[j]==0) t/=pri[j],tmi[j]++;
			} 
		}
		if(t>1) continue;
		for(int j=1; j<=cnt; j++) {
			t=len[j];
			for(int k=1; k<=pn; k++) _tmi[k]=0;
			for(int k=1; k<=pn; k++) if(t%pri[k]==0) {
				while(t%pri[k]==0) t/=pri[k],_tmi[k]++;
			} 
			int flag=1;
			for(int k=1; k<=pn; k++) if(_tmi[k]>tmi[k]) flag=0;
			if(flag) sum+=len[j];
//			cout<<sum<<endl;
		}
		f[i]=qpow(1ll*sum*qpow(m,mod-2)%mod,n);
//		cout<<f[i]<<" ";
	} 
//	cout<<endl;
}
main() {
	cin>>n>>m;
	for(int i=1; i<=m; i++) cin>>p[i];
	for(int i=1; i<=m; i++) if(!vis[i]) {
		int t=i; vis[t]=1,len[++cnt]=1;
		while(p[t]!=i) t=p[t],vis[t]=1,len[cnt]++;
	}
//	for(int i=1; i<=cnt; i++) cout<<len[i]<<" ";
//	cout<<endl;
	memset(vis,0,sizeof(vis));
	for(int i=2; i<=210; i++) {
		if(!vis[i]) pri[++pn]=i;
		for(int j=1; j<=pn; j++) {
			vis[i*pri[j]]=1;
			if(i%pri[j]==0) break;
		}
	}
//	for(int i=1; i<=pn; i++) cout<<pri[i]<<" ";
//	cout<<endl;
	calc();
//	puts("1");
	for(int i=1; i<=V; i++) if(f[i]) {
		for(int j=2; i*j<=V; j++) if(f[i*j]) f[i*j]=(f[i*j]-f[i]+mod)%mod;
//		cout<<i<<endl;
	}
//	for(int i=1; i<=V; i++) cout<<f[i]<<" ";
//	cout<<endl; 
//	puts("1");
	for(int i=1; i<=V; i++) ans=(ans+1ll*f[i]*i%mod)%mod;
	ans=1ll*ans*qpow(m,n)%mod;
	cout<<ans;
}
