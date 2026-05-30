#include<bits/stdc++.h>
#define N 100010
using namespace std;
int T,n,m,a[N],k[N],buc[N];
int main() {
	cin>>T;
	while(T--) {
		int sum=0,cnt=0;
		cin>>n>>m;
		for(int i=1; i<=m; i++) buc[i]=0;
		for(int i=1; i<=n; i++) cin>>a[i];
		for(int i=1; i<=m; i++) {
			cin>>k[i];
			sum+=k[i];
		}
		for(int i=1; i<=sum; i++) buc[a[i]]++;
		for(int i=1; i<=m; i++) if(buc[i]==k[i]) cnt++;
		int flag=0;
		if(cnt==m) flag=1;
		for(int i=sum+1; i<=n; i++) {
			buc[a[i-sum]]--;
			if(buc[a[i-sum]]+1==k[a[i-sum]]) cnt--;
			if(buc[a[i-sum]]==k[a[i-sum]]) cnt++;
			buc[a[i]]++;
			if(buc[a[i]]-1==k[a[i]]) cnt--;
			if(buc[a[i]]==k[a[i]]) cnt++;
			if(cnt==m) flag=1;
		}
		if(flag) puts("YES");
		else puts("NO");
	}	
}
