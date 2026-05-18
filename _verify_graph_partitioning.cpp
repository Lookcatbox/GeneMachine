#include <bits/stdc++.h>
using namespace std;
long long brute(int n, const vector<pair<int,int>>& edges){
    int m=edges.size();
    long long ans=0;
    for(int mask=0; mask<(1<<m); ++mask){
        vector<int> redIn(n+1), blueOut(n+1);
        set<pair<int,int>> red, blue;
        bool ok=true;
        for(int i=0;i<m;i++){
            auto [u,v]=edges[i];
            int a=min(u,v), b=max(u,v);
            if((mask>>i)&1){
                if(!blue.insert({a,b}).second){ok=false;break;}
                blueOut[a]++;
            }else{
                if(!red.insert({a,b}).second){ok=false;break;}
                redIn[b]++;
            }
        }
        if(!ok) continue;
        if(redIn[1]!=0) continue;
        for(int i=2;i<=n;i++) if(redIn[i]!=1) ok=false;
        for(int i=1;i<n;i++) if(blueOut[i]!=1) ok=false;
        if(blueOut[n]!=0) ok=false;
        ans += ok;
    }
    return ans;
}

const int MOD=998244353;
long long solve(int n, const vector<pair<int,int>>& edges){
    unordered_map<long long,int> mp;
    auto key=[&](int a,int b){ return 1LL*a*(n+1)+b; };
    for(auto [u,v]:edges){
        if(u==v) return 0;
        int a=min(u,v), b=max(u,v);
        long long k=key(a,b);
        if(++mp[k]>2) return 0;
    }
    vector<int> dL(n+1), dR(n+1);
    vector<pair<int,int>> singles;
    long long ans=1;
    for(auto &it: mp){
        int a=it.first/(n+1), b=it.first%(n+1), c=it.second;
        if(c==2){
            ans*=2;
            dR[a]++; dL[b]++;
        }else singles.push_back({a,b});
    }
    for(int i=1;i<=n;i++) if(dL[i]>1 || dR[i]>1) return 0;
    vector<int> needL(n+1), needR(n+1);
    for(int i=2;i<=n;i++) needL[i]=1-dL[i];
    for(int i=1;i<n;i++) needR[i]=1-dR[i];
    vector<int> knownBlue(n+1), knownRed(n+1);
    vector<pair<int,int>> flex;
    for(auto [a,b]:singles){
        if(!needR[a] && !needL[b]) return 0;
        else if(!needR[a] && needL[b]) knownRed[b]++;
        else if(needR[a] && !needL[b]) knownBlue[a]++;
        else flex.push_back({a,b});
    }
    for(int i=1;i<n;i++) if(knownBlue[i]>needR[i]) return 0;
    for(int i=2;i<=n;i++) if(knownRed[i]>needL[i]) return 0;
    vector<int> remBlue=needR, remRed=needL;
    for(int i=1;i<n;i++) remBlue[i]-=knownBlue[i];
    for(int i=2;i<=n;i++) remRed[i]-=knownRed[i];
    int m=flex.size();
    vector<vector<int>> adjT(n+1), adjH(n+1);
    vector<int> U(m), V(m), degT(n+1), degH(n+1), state(m);
    for(int i=0;i<m;i++){
        U[i]=flex[i].first; V[i]=flex[i].second;
        adjT[U[i]].push_back(i); adjH[V[i]].push_back(i);
        degT[U[i]]++; degH[V[i]]++;
    }
    deque<pair<int,int>> q;
    vector<char> inqT(n+1), inqH(n+1);
    auto pushT=[&](int x){ if(!inqT[x]) inqT[x]=1, q.push_back({0,x}); };
    auto pushH=[&](int x){ if(!inqH[x]) inqH[x]=1, q.push_back({1,x}); };
    for(int i=1;i<n;i++) pushT(i);
    for(int i=2;i<=n;i++) pushH(i);
    auto setRed=[&](int e)->bool{
        if(state[e]==1) return true;
        if(state[e]==2) return false;
        state[e]=1;
        remRed[V[e]]--;
        degT[U[e]]--; degH[V[e]]--;
        pushT(U[e]); pushH(V[e]);
        return true;
    };
    auto setBlue=[&](int e)->bool{
        if(state[e]==2) return true;
        if(state[e]==1) return false;
        state[e]=2;
        remBlue[U[e]]--;
        degT[U[e]]--; degH[V[e]]--;
        pushT(U[e]); pushH(V[e]);
        return true;
    };
    while(!q.empty()){
        auto [tp,x]=q.front(); q.pop_front();
        if(tp==0){
            inqT[x]=0;
            if(remBlue[x]<0 || remBlue[x]>degT[x]) return 0;
            if(degT[x]==0){ if(remBlue[x]!=0) return 0; continue; }
            if(remBlue[x]==0){
                for(int e:adjT[x]) if(state[e]==0 && !setRed(e)) return 0;
            }else if(remBlue[x]==degT[x]){
                for(int e:adjT[x]) if(state[e]==0 && !setBlue(e)) return 0;
            }
        }else{
            inqH[x]=0;
            if(remRed[x]<0 || remRed[x]>degH[x]) return 0;
            if(degH[x]==0){ if(remRed[x]!=0) return 0; continue; }
            if(remRed[x]==0){
                for(int e:adjH[x]) if(state[e]==0 && !setBlue(e)) return 0;
            }else if(remRed[x]==degH[x]){
                for(int e:adjH[x]) if(state[e]==0 && !setRed(e)) return 0;
            }
        }
    }
    vector<char> visT(n+1), visH(n+1);
    long long cyc=0;
    for(int s=1;s<n;s++) if(degT[s] && !visT[s]){
        cyc++;
        deque<pair<int,int>> dq;
        dq.push_back({0,s}); visT[s]=1;
        long long vc=0, ec=0;
        while(!dq.empty()){
            auto [tp,x]=dq.front(); dq.pop_front();
            vc++;
            if(tp==0){
                if(remBlue[x]!=1 || degT[x]<2) return 0;
                for(int e:adjT[x]) if(state[e]==0){
                    ec++;
                    int y=V[e];
                    if(!visH[y]) visH[y]=1, dq.push_back({1,y});
                }
            }else{
                if(remRed[x]!=1 || degH[x]<2) return 0;
                for(int e:adjH[x]) if(state[e]==0){
                    int y=U[e];
                    if(!visT[y]) visT[y]=1, dq.push_back({0,y});
                }
            }
        }
        if(ec!=vc) return 0;
    }
    while(cyc--) ans = ans*2;
    return ans;
}

int main(){
    for(int n=1;n<=6;n++){
        vector<pair<int,int>> all;
        for(int i=1;i<=n;i++) for(int j=i+1;j<=n;j++) all.push_back({i,j});
        if((int)all.size()>6) break;
        vector<int> choose(2*n-2);
        function<void(int,int,vector<pair<int,int>>&)> dfs=[&](int idx,int last,vector<pair<int,int>>& cur){
            if(idx==2*n-2){
                long long b=brute(n,cur), s=solve(n,cur);
                if(b!=s){
                    cerr<<"mismatch n="<<n<<" brute="<<b<<" solve="<<s<<" edges:";
                    for(auto [u,v]:cur) cerr<<" ("<<u<<","<<v<<")";
                    cerr<<"\n";
                    exit(0);
                }
                return;
            }
            for(int i=last;i<(int)all.size();i++){
                int cnt=0;
                for(auto &e:cur) if(e==all[i]) cnt++;
                if(cnt==2) continue;
                cur.push_back(all[i]);
                dfs(idx+1,i,cur);
                cur.pop_back();
            }
        };
        vector<pair<int,int>> cur;
        dfs(0,0,cur);
        cerr<<"n="<<n<<" ok\n";
    }
    cerr<<"all ok\n";
}
