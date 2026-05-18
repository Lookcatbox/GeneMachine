### 自动上传脚本

修改脚本中

```bash

git config user.name "linkychristian"
git config user.email "linkychristian90@gmail.com"

``` 

这两行，登录github后，打开PowerShell，使用

```bash

Set-Location "项目目录"

Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

.\auto-upload-git.ps1 \\默认上传

.\auto-upload-git.ps1 -Message "你的提交说明" \\自定义提交信息

.\auto-upload-git.ps1 -Branch "dev" -Remote "origin" \\自定义分支和远程仓库

``` 