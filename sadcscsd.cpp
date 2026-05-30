#include<bits/stdc++.h>
#define N 200010
#define int long long
using namespace std;
int n,m,a[N],s[N];
main() {
	cin>>n>>m;
	for(int i=1; i<=n; i++) cin>>a[i];
	sort(a+1,a+n+1);
	for(int i=1; i<=n; i++) s[i]=s[i-1]+a[i];
	for(int i=1; i<=m; i++) {
		int h; cin>>h;
		int pos=upper_bound(a+1,a+n+1,h)-a;
		cout<<s[pos-1]+1ll*(n-pos+1)*h<<endl;
	}
}
