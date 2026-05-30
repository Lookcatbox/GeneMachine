#include<bits/stdc++.h>
#define N 200010
#define int long long
using namespace std;
int T,n,m,mx,mn;
int a[N],b[N],nd1[N],nd0[N];
main() {
	cin>>T;
//	cout<<log2(1000000000);
	while(T--) {
		cin>>n>>m;mx=0,mn=2e9+7;
		for(int i=1; i<=n; i++) cin>>a[i];
		for(int i=1; i<=m; i++) cin>>b[i];
		for(int k=30; ~k; k--) {
			int sum=0;
			for(int i=1; i<=n; i++) if(a[i]&(1ll<<k)) sum++;
			if((sum&1)==(n&1)) nd1[k]=nd0[k]=2;
			else if(sum&1) nd1[k]=0,nd0[k]=1;
			else nd1[k]=1,nd0[k]=0;
		}
		for(int i=30; ~i; i--) {
			int s1=0,s0=0,ns1=0,ns0=0,as1=0,as0=0;
			for(int k=30; k>=i; k--) s1|=((nd1[i]>0)<<k),s0|=((nd0[i]>0)<<k),ns1|=((nd1[i]==1)<<k),ns0|=((nd0[i]==1)<<k);
			s1|=(1ll<<i)-1,s0|=(1ll<<i)-1;
			for(int j=1; j<=m; j++) {
				if((b[j]|s1)==s1) as1|=b[j];
				if((b[j]|s0)==s0) as0|=b[j];
			}
			if((ns1|as1)==as1) {
				int res=0;
				for(int j=1; j<=n; j++) res^=a[j]|as1;
				mx=max(mx,res);
			}
			if((ns0|as0)==as0) {
				int res=0;
				for(int j=1; j<=n; j++) res^=a[j]|as0;
				mn=min(mn,res);
			}
		}
		cout<<mn<<" "<<mx<<endl;
	}
}
