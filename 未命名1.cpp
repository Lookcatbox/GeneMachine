#include<bits/stdc++.h>
using namespace std;
int n,a[21][21],len[21],f[21][100010];
long long mod=1000000001,ans=1;
void work(int x) {
	a[1][1]=x;
	int nn=2;
	for(len[1]=2; ; len[1]++) {
		a[1][len[1]]=a[1][len[1]-1]*2;
		if(a[1][len[1]]>n) break;
	}len[1]--;
//	int mx=len[1];
	for(; ; nn++) {
		a[nn][1]=a[nn-1][1]*3;
		if(a[nn][1]>n) break;
		for(len[nn]=2; ; len[nn]++) {
			a[nn][len[nn]]=a[nn-1][len[nn]]*3;
			if(a[nn][len[nn]]>n) break;
		}len[nn]--;

	}nn--;

    for(int i=0; i<(1<<len[1]); i++) f[1][i]=!((i<<1)&(i));
    for(int i=2; i<=nn; i++) {
    	for(int j=0; j<(1<<len[i]); j++) {
    		if((j<<1)&(j)) continue;
    		f[i][j]=0;
    		for(int k=0; k<(1<<len[i-1]); k++)
    		    if((!((k<<1)&(k)))&&((k&j)==0)) f[i][j]=(f[i][j]+f[i-1][k])%mod;	    
		}
	}
	long long num=0;
	for(int i=0; i<(1<<len[nn]); i++) num=(num+f[nn][i])%mod;
	ans=ans*num%mod;
}
int main()
{
	int T; cin>>T;
	while(T--) {
		cin>>n;ans=1;
		memset(a,0,sizeof(a));
		memset(len,0,sizeof(len));
		memset(f,0,sizeof(f));
		for(int i=1; i<=n; i++) 
			if((i%2)&&(i%3)) work(i);
		cout<<ans<<endl;	
	} 
	return 0;
}

