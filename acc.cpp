#include<bits/stdc++.h>
#define N 5010
#define mod 1000000007
using namespace std;
int T,n,a[N],pre[N][2],f[N][N],b[2],mp[N];
void add(int& a1,int a2) {a1=(a1+a2)%mod;}
int main() {
	cin>>T;
	while(T--) {
		cin>>n;b[0]=b[1]=0;
		for(int i=1; i<=n; i++) {cin>>a[i];pre[i][0]=pre[i][1]=0;mp[i]=(++b[a[i]&1]);}
		for(int i=1; i<=n; i++) {
			for(int j=1; j<i; j++) if(((a[i]&1)^(a[j]&1))&&abs(a[i]-a[j])>1) 
				pre[mp[i]][a[i]&1]=mp[j];
		}
		for(int i=0; i<=n; i++)
			for(int j=0; j<=n; j++) f[i][j]=0;
		f[0][0]=1;
		
		for(int i=0; i<=b[0]; i++)
			for(int j=0; j<=b[1]; j++) {
//				cout<<i<<" "<<j<<" "<<f[i][j]<<endl;
				if(j<b[1]&&i>=pre[j+1][1]) add(f[i][j+1],f[i][j]);
				if(i<b[0]&&j>=pre[i+1][0]) add(f[i+1][j],f[i][j]); 
			}
		cout<<f[b[0]][b[1]]<<endl;
	}
} 
