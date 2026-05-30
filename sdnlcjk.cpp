#include<bits/stdc++.h>
#define N 1010
using namespace std;
int n,m;
string s[N],ans;
int main() {
	cin>>n>>m;
	for(int i=1; i<=n; i++) cin>>s[i];
	for(int j=0; j<m; j++) {
		int t[2]={0,0};
		for(int i=1; i<=n; i++) {
			t[s[i][j]-'0']++;
		}
		if(t[1]>t[0]) ans+='1';
		else ans+='0';
	} cout<<ans;
}
