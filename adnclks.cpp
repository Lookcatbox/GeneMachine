#include<bits/stdc++.h>
using namespace std;
int T,d[4][3][2]={{{-1,-1},{0,0},{1,1}},{{-1,1},{0,0},{1,-1}},{{0,-1},{0,0},{0,1}},{{1,0},{0,0},{-1,0}}};
string s[5];
int main() {
	cin>>T;
	while(T--) {
		cin>>s[0]>>s[1]>>s[2];
		int flag=0;
		char ans;
		for(int i=0; i<3; i++) {
			for(int j=0; j<3; j++) {
				for(int k=0; k<4; k++) {
					int ttt=0,ff=1;
					for(int l=0; l<3; l++) {
						int tx=i+d[k][l][0],ty=j+d[k][l][1];
						if(tx<0||tx>2||ty<0||ty>2) {
							ff=0;
							break;
						}
						if(!ttt) ttt=s[tx][ty];
						else {
							if(s[tx][ty]!=ttt) ff=0;
						}
					}
					if(ff) {flag=1; ans=s[i][j];}
				}
			}
		}
		if(flag&&ans!='.') {cout<<ans<<endl;}
		else {puts("DRAW");}
	}	
}
